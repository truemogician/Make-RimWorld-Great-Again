using System.Collections.Generic;
using System.Linq;
using RimWorld;
using TrueMogician.RimWorld.Rimsonable.Patches;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.Rimsonable.Components;

public sealed class EmergencyJobDispatchTracker(Map map) : MapComponent(map) {
	private const int _REFRESH_INTERVAL_TICKS = 60;

	private readonly HashSet<int> _dispatchedPawnIds = [];

	public bool IsDispatched(Pawn pawn) => _dispatchedPawnIds.Contains(pawn.thingIDNumber);

	public void Clear() => _dispatchedPawnIds.Clear();

	public override void MapComponentTick() {
		if (!Settings.Default[Features.EmergencyJobOverride]) {
			if (_dispatchedPawnIds.Count > 0)
				_dispatchedPawnIds.Clear();
			return;
		}
		// Stagger refresh across maps so they don't all recompute on the same tick.
		int phase = map.uniqueID % _REFRESH_INTERVAL_TICKS;
		if (Find.TickManager.TicksGame % _REFRESH_INTERVAL_TICKS != phase)
			return;
		Refresh();
	}

	public void Refresh() {
		_dispatchedPawnIds.Clear();
		var emergencyWGs = EmergencyJobOverride.EmergencyWorkGivers;
		if (emergencyWGs.Count == 0)
			return;
		var candidates = map.mapPawns.FreeColonistsSpawned
			.Where(pawn => pawn is { Drafted: false, Downed: false, InMentalState: false } && !pawn.IsBurning())
			.ToList();
		if (candidates.Count == 0)
			return;
		// Defer override triggers until after the read phase: StartJob side-effects can mutate listerThings / mapPawns.
		var scanners = emergencyWGs
			.Select(wg => wg.Worker)
			.OfType<WorkGiver_Scanner>()
			.ToList();
		var pendingOverrides = scanners.SelectMany(scanner => DispatchForWorkGiver(scanner, candidates)).ToList();
		foreach (var pawn in pendingOverrides)
			pawn.jobs?.CheckForJobOverride();
	}

	internal static bool IsInterruptible(Pawn pawn) {
		if (pawn.CurJob is not { } job
			|| pawn.mindState.IsIdle                                              // wandering / idle waits (vanilla JobTag.Idle)
			|| job.def.joyKind is not null                                        // joy + meditation
			|| (pawn.GetPosture() != PawnPosture.Standing && !pawn.Deathresting)) // sleeping, resting, sitting; exempt deathrest
			return true;
		return Settings.Default.EmergencyJobInterruptOngoingWork && job.def.casualInterruptible;
	}

	private static bool PawnCanUseWorkGiver(Pawn pawn, WorkGiver_Scanner scanner) {
		var def = scanner.def;
		if (!def.nonColonistsCanDo && pawn is { IsColonist: false, IsColonyMech: false, IsColonySubhuman: false })
			return false;
		if (def.workTags != WorkTags.None && pawn.WorkTagIsDisabled(def.workTags))
			return false;
		if (def.workType != null && pawn.WorkTypeIsDisabled(def.workType))
			return false;
		if (scanner.ShouldSkip(pawn))
			return false;
		if (scanner.MissingRequiredCapacity(pawn) != null)
			return false;
		return true;
	}

	private IEnumerable<Pawn> DispatchForWorkGiver(WorkGiver_Scanner scanner, List<Pawn> candidates) {
		var eligible = candidates.Where(c => IsInterruptible(c) && PawnCanUseWorkGiver(c, scanner)).ToList();
		if (eligible.Count == 0)
			yield break;
		if (scanner.def.scanThings) {
			// Mirror JobGiver_Work: prefer PotentialWorkThingsGlobal, fall back to ListerThings; both can return live lists, so snapshot below.
			var targets = scanner.PotentialWorkThingsGlobal(eligible[0]);
			if (targets is null) {
				if (scanner.PotentialWorkThingRequest.IsUndefined)
					yield break;
				targets = map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest);
			}
			foreach (var target in targets.ToList()) {
				var closest = FindClosestCapable(scanner, target, eligible);
				if (closest is null)
					continue;
				if (_dispatchedPawnIds.Add(closest.thingIDNumber))
					yield return closest;
			}
		}
		else { // NonScanJob-only WGs (e.g. WorkGiver_PatientGoToBedEmergencyTreatment): the calling pawn IS the target.
			foreach (var candidate in eligible) {
				if (_dispatchedPawnIds.Contains(candidate.thingIDNumber))
					continue;
				Job? job = null;
				try {
					job = scanner.NonScanJob(candidate);
				}
				catch {
					// Swallow WorkGiver-internal exceptions; vanilla logs its own.
				}
				if (job is not null && _dispatchedPawnIds.Add(candidate.thingIDNumber))
					yield return candidate;
			}
		}
	}

	private Pawn? FindClosestCapable(WorkGiver_Scanner scanner, Thing target, List<Pawn> eligible) {
		Pawn? best = null;
		var bestDistSq = int.MaxValue;
		foreach (var candidate in eligible) {
			if (_dispatchedPawnIds.Contains(candidate.thingIDNumber))
				continue;
			try {
				if (!scanner.HasJobOnThing(candidate, target))
					continue;
			}
			catch {
				continue;
			}
			if (!Settings.Default.EmergencyJobIgnoreAllowedArea
				&& candidate.playerSettings?.AreaRestrictionInPawnCurrentMap is { } area
				&& !area[target.Position])
				continue;
			int distSq = (candidate.Position - target.Position).LengthHorizontalSquared;
			if (distSq < bestDistSq) {
				best = candidate;
				bestDistSq = distSq;
			}
		}
		return best;
	}
}