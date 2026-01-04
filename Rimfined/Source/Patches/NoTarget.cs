using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.Rimfined.Patches;

internal class NoTargetPatches {
	private static System.WeakReference<Game>? _curGame;

	internal static Texture2D NoTargetIcon => field ??= ContentFinder<Texture2D>.Get("UI/Commands/NoTarget");

	internal static NoTargetGameComponent Component {
		get {
			_curGame ??= new System.WeakReference<Game>(Current.Game);
			if (_curGame.TryGetTarget(out var game) && Current.Game == game)
				return field ??= game.GetComponent<NoTargetGameComponent>();
			var newGame = Current.Game;
			_curGame.SetTarget(newGame);
			return field = newGame.GetComponent<NoTargetGameComponent>();
		}
	}

	[HarmonyPatch(typeof(AttackTargetFinder), nameof(AttackTargetFinder.IsAutoTargetable))]
	[HarmonyPrefix]
	internal static bool AttackTargetFinder_IsAutoTargetable_Prefix(IAttackTarget target, ref bool __result) {
		if (target?.Thing is Pawn pawn && Component[pawn]) {
			__result = false;
			return false;
		}
		return true;
	}

	// Add toggle gizmo on hostile pawns
	[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
	[HarmonyPostfix]
	internal static IEnumerable<Gizmo> Pawn_GetGizmos_Postfix(IEnumerable<Gizmo> __result, Pawn __instance) {
		foreach (var g in __result)
			yield return g;
		if (__instance is not { Spawned: true, Dead: false } pawn)
			yield break;
		if (pawn.Faction is not { } f || !f.HostileTo(Faction.OfPlayer))
			yield break;
		yield return new Command_Toggle {
			defaultLabel = "No Target",
			defaultDesc = "Prevent colonists and turrets from auto-targeting this pawn.",
			icon = NoTargetIcon,
			isActive = () => Component[pawn],
			toggleAction = () => Component.Toggle(pawn)
		};
	}

	// --- Auto-mark: hostile pawns with relationships to any spawned free colonist
	[HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
	[HarmonyPostfix]
	internal static void Pawn_SpawnSetup_Postfix(Pawn __instance, bool respawningAfterLoad) {
		if (respawningAfterLoad)
			return;
		if (__instance is not { Dead: false })
			return;
		if (__instance.Faction is not { } f || !f.HostileTo(Faction.OfPlayer))
			return;
		if (Component[__instance])
			return;
		if (HasRelationshipWithAnyColonist(__instance))
			Component[__instance] = true;
	}

	private static bool HasRelationshipWithAnyColonist(Pawn pawn) {
		if (pawn.relations is not { } rel)
			return false;

		// Cheap & safe: only check currently spawned free colonists (usually small list)
		foreach (var colonist in PawnsFinder.AllMaps_FreeColonistsSpawned) {
			if (colonist is null)
				continue;
			// Check both directions; direct relations aren't guaranteed to be symmetric in-memory
			if (rel.DirectRelations.Any(r => r.otherPawn == colonist))
				return true;
			if (colonist.relations is { } cRel && cRel.DirectRelations.Any(r => r.otherPawn == pawn))
				return true;
		}

		return false;
	}
}

public sealed class NoTargetGameComponent : GameComponent {
	private HashSet<string> _noTargetPawnIds = new(StringComparer.Ordinal);

	public bool this[Pawn pawn] {
		get => _noTargetPawnIds.Contains(pawn.GetUniqueLoadID());
		set {
			var id = pawn.GetUniqueLoadID();
			if (value)
				_noTargetPawnIds.Add(id);
			else
				_noTargetPawnIds.Remove(id);
		}
	}

	public void Toggle(Pawn pawn) {
		var id = pawn.GetUniqueLoadID();
		if (!_noTargetPawnIds.Add(id))
			_noTargetPawnIds.Remove(id);
	}

	public override void ExposeData() {
		Scribe_Collections.Look(ref _noTargetPawnIds, "noTargetPawnIds", LookMode.Value);
		if (Scribe.mode == LoadSaveMode.PostLoadInit)
			_noTargetPawnIds ??= new HashSet<string>(StringComparer.Ordinal);
	}
}