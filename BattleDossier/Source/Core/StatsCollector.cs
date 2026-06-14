using RimWorld;
using TrueMogician.RimWorld.BattleDossier.Components;
using TrueMogician.RimWorld.BattleDossier.Models;
using Verse;

namespace TrueMogician.RimWorld.BattleDossier.Core;

/// <summary>
///     Turns combat hooks into log entries: damage becomes <see cref="HitLog" />s, captures become
///     <see cref="CasualtyLog" />s. Casualties from damage (downs/kills) are recorded from battle-log state
///     transitions instead. Resolves credit through the attribution pipeline and books it into the right session.
/// </summary>
internal static class StatsCollector {
	internal static void OnDamageDealt(Thing victim, in DamageInfo dinfo, DamageWorker.DamageResult result) {
		if (DossierManager.Instance is not { } manager)
			return;
		if (result.totalDamageDealt <= 0f || !dinfo.Def.ExternalViolenceFor(victim) || !IsTrackableVictim(victim))
			return;
		var context = new AttributionContext { Victim = victim, Dinfo = dinfo, Result = result, Kind = AttributionKind.Damage };
		var credited = AttributionResolver.Resolve(in context);
		var session = (credited != null ? manager.FindSessionFor(credited) : null) ?? manager.FindSessionFor(victim);
		if (session == null)
			return;
		session.NoticeActivity();
		session.NoticeMap(victim.MapHeld);
		session.GetOrAddParticipant(victim);

		var instigatorId = -1;
		var hostile = false;
		if (credited != null && credited != victim) {
			session.GetOrAddParticipant(credited);
			instigatorId = credited.thingIDNumber;
			hostile = AreHostile(credited, victim);
		}
		session.AddHit(instigatorId, victim.thingIDNumber, result.totalDamageDealt, hostile);
		if (victim is Building { Destroyed: true })
			session.AddCasualty(CasualtyType.Destroyed, victim.thingIDNumber, instigatorId, hostile);
	}

	internal static void OnCaptured(Pawn captured, Faction by, Pawn? captor) {
		if (DossierManager.Instance is not { } manager || !by.IsPlayer)
			return;
		if (manager.FindSessionFor(captured) is not { } session)
			return;
		session.GetOrAddParticipant(captured);
		var sourceId = -1;
		var hostile = false;
		if (captor != null) {
			session.GetOrAddParticipant(captor);
			sourceId = captor.thingIDNumber;
			hostile = AreHostile(captor, captured);
		}
		session.AddCasualty(CasualtyType.Captured, captured.thingIDNumber, sourceId, hostile);
	}

	/// <summary>
	///     Faction-based hostility, robust to a combatant being destroyed at check time (vanilla
	///     <see cref="GenHostility.HostileTo(Thing, Thing)" /> short-circuits to false then). A factionless
	///     combatant in a battle counts as hostile to any faction it is not part of.
	/// </summary>
	internal static bool AreHostile(Thing a, Thing b) {
		if (a == b)
			return false;
		var fa = a.Faction;
		var fb = b.Faction;
		return fa != null && fb != null ? fa.HostileTo(fb) : fa != fb;
	}

	private static bool IsTrackableVictim(Thing victim) => victim is Pawn or Building_Turret or Building_Trap;
}