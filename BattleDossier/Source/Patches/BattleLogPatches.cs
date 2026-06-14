using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.BattleDossier.Components;
using TrueMogician.RimWorld.BattleDossier.Core;
using TrueMogician.RimWorld.BattleDossier.Models;
using Verse;

namespace TrueMogician.RimWorld.BattleDossier.Patches;

/// <summary>
///     Session lifecycle and casualty feed: starts/extends a session when the player is in hostile combat,
///     consolidates sessions bridged by vanilla battle absorption, and turns state-transition log entries
///     (downs/kills) into <see cref="CasualtyLog" />s carrying the vanilla prose + the attributed source.
/// </summary>
[HarmonyPatch(typeof(BattleLog))]
internal static class BattleLogPatches {
	private static readonly AccessTools.FieldRef<BattleLogEntry_StateTransition, RulePackDef> _transitionDefField =
		AccessTools.FieldRefAccess<BattleLogEntry_StateTransition, RulePackDef>("transitionDef");

	private static readonly AccessTools.FieldRef<BattleLogEntry_StateTransition, Pawn> _subjectPawnField =
		AccessTools.FieldRefAccess<BattleLogEntry_StateTransition, Pawn>("subjectPawn");

	private static readonly AccessTools.FieldRef<BattleLogEntry_StateTransition, Pawn> _initiatorField =
		AccessTools.FieldRefAccess<BattleLogEntry_StateTransition, Pawn>("initiator");

	[HarmonyPatch(nameof(BattleLog.Add))]
	[HarmonyPostfix]
	internal static void Add_Postfix(LogEntry entry) {
		if (DossierManager.Instance is not { } manager)
			return;
		var concerned = entry.GetConcerns().OfType<Pawn>().ToList();
		if (concerned.Count == 0)
			return;
		var battle = concerned.Select(p => p.records.BattleActive).FirstOrDefault(b => b != null);
		if (battle == null)
			return;
		var session = manager.FindSession(battle);
		if (session == null) {
			session = ResolveNewSession(manager, battle, concerned);
			if (session == null)
				return;
		}
		else {
			session.AddBattle(battle, concerned[0].MapHeld);
			manager.ConsolidateSessions();
			// Consolidation may have merged this session into another; re-resolve before touching it.
			session = manager.FindSession(battle);
			if (session == null)
				return;
		}
		session.NoticeActivity();
		foreach (var pawn in concerned)
			session.GetOrAddParticipant(pawn);
		if (entry is BattleLogEntry_StateTransition transition)
			RecordTransition(session, transition);
	}

	private static BattleSession? ResolveNewSession(DossierManager manager, Battle battle, List<Pawn> concerned) {
		var map = concerned[0].MapHeld;
		if (ShouldStart(concerned, map))
			return manager.StartSession(battle, map);
		// A new engagement that fails the start gate (a skirmish, or combat not directly involving the player)
		// is still folded into an ongoing battle on the same map rather than dropped — only a separate dossier is withheld.
		if (manager.SessionOnMap(map) is { } active) {
			active.AddBattle(battle, map);
			return active;
		}
		return null;
	}

	private static bool ShouldStart(List<Pawn> concerned, Map? map) {
		var hostileCount = 0;
		var hostilePower = 0f;
		if (map != null) {
			// Attack-target cache rather than faction checks, so aggressive wildlife (manhunters, predators)
			// counts toward scale just like faction raiders.
			foreach (var target in map.attackTargetsCache.TargetsHostileToColony) {
				if (target.Thing is Pawn pawn) {
					hostileCount++;
					hostilePower += pawn.kindDef.combatPower;
				}
			}
		}
		var context = new BattleStartContext {
			Concerned = concerned,
			Map = map,
			HostileCount = hostileCount,
			HostileCombatPower = hostilePower,
			MinScale = Settings.Default.MinBattleScale
		};
		return BattleStartConditions.Evaluate(in context);
	}

	private static void RecordTransition(BattleSession session, BattleLogEntry_StateTransition transition) {
		if (_subjectPawnField(transition) is not { } subject)
			return;
		var initiator = _initiatorField(transition);
		bool isDown = _transitionDefField(transition) == RulePackDefOf.Transition_Downed;
		var kind = isDown ? CasualtyType.Downed : CasualtyType.Killed;
		// Render the prose now, while the pawns still exist — the grammar needs them alive.
		string prose = transition.ToGameStringFromPOV(null);

		var sourceId = -1;
		var hostile = false;
		if (initiator != null && StatsCollector.AreHostile(initiator, subject)) {
			session.GetOrAddParticipant(initiator);
			sourceId = initiator.thingIDNumber;
			hostile = true;
		}
		else if (isDown && session.LastHostileHitter(subject.thingIDNumber) is var last && last >= 0) {
			// Blood-loss downs reach here with no hostile initiator; credit the last hostile hitter.
			sourceId = last;
			hostile = true;
		}
		session.AddCasualty(kind, subject.thingIDNumber, sourceId, hostile, prose);
	}
}