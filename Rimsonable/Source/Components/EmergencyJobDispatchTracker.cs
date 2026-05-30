using System.Collections.Generic;
using System.Linq;
using RimWorld;
using TrueMogician.RimWorld.Rimsonable.Patches;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.Rimsonable.Components;

public sealed class EmergencyJobDispatchTracker(Map map) : MapComponent(map) {
	private const int _REFRESH_INTERVAL_TICKS = 60;

	internal HashSet<int> DispatchedPawnIds { get; } = [];

	private List<WorkGiver_Scanner> EmergencyWorkGiverScanners =>
		field ??= EmergencyJobOverride.EmergencyWorkGivers
			.Select(wg => wg.Worker)
			.OfType<WorkGiver_Scanner>()
			.ToList();

	public override void MapComponentTick() {
		if (!Settings.Default[Features.EmergencyJobOverride]) {
			if (DispatchedPawnIds.Count > 0)
				DispatchedPawnIds.Clear();
			return;
		}
		// Stagger refresh across maps so they don't all recompute on the same tick.
		int phase = map.uniqueID % _REFRESH_INTERVAL_TICKS;
		if (Find.TickManager.TicksGame % _REFRESH_INTERVAL_TICKS != phase)
			return;
		Refresh();
	}

	public void Refresh() {
		DispatchedPawnIds.Clear();
		if (EmergencyJobOverride.EmergencyWorkGivers.Count == 0)
			return;
		var candidates = map.mapPawns.FreeColonistsSpawned
			.Where(pawn => pawn is { Drafted: false, Downed: false, InMentalState: false } && !pawn.IsBurning() && IsInterruptible(pawn))
			.ToList();
		if (candidates.Count == 0)
			return;
		// Defer override triggers until after the read phase: StartJob side-effects can mutate listerThings / mapPawns.
		var pendingOverrides = EmergencyWorkGiverScanners.SelectMany(scanner => DispatchForWorkGiver(scanner, candidates)).ToList();
		foreach (var pawn in pendingOverrides) {
			if (pawn.CurJob?.workGiverDef?.emergency == true)
				continue;
			pawn.jobs?.CheckForJobOverride();
		}
	}

	internal static bool IsInterruptible(Pawn pawn) =>
		pawn.CurJob is not { } job
		|| pawn.mindState.IsIdle                                             // wandering / idle waits (vanilla JobTag.Idle)
		|| job.def.joyKind is not null                                       // joy + meditation
		|| (pawn.GetPosture() != PawnPosture.Standing && !pawn.Deathresting) // sleeping, resting, sitting; exempt deathrest
		|| job.jobGiver is JobGiver_SeekAllowedArea                          // the "go home" job is itself only there because we sent them out
		|| (Settings.Default.EmergencyJobInterruptOngoingWork && job.def.casualInterruptible);

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
		var eligible = candidates.Where(c => !DispatchedPawnIds.Contains(c.thingIDNumber) && PawnCanUseWorkGiver(c, scanner)).ToList();
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
				if (DispatchedPawnIds.Add(closest.thingIDNumber))
					yield return closest;
			}
		}
		else { // NonScanJob-only WGs (e.g. WorkGiver_PatientGoToBedEmergencyTreatment): the calling pawn IS the target.
			foreach (var candidate in eligible) {
				Job? job = null;
				try {
					job = scanner.NonScanJob(candidate);
				}
				catch {
					// Swallow WorkGiver-internal exceptions; vanilla logs its own.
				}
				if (job is not null && DispatchedPawnIds.Add(candidate.thingIDNumber))
					yield return candidate;
			}
		}
	}

	private Pawn? FindClosestCapable(WorkGiver_Scanner scanner, Thing target, List<Pawn> eligible) {
		var respectAllowedArea = !Settings.Default.EmergencyJobIgnoreAllowedArea;
		foreach (var candidate in eligible.OrderBy(c => (c.Position - target.Position).LengthHorizontalSquared)) {
			try {
				if (!scanner.HasJobOnThing(candidate, target))
					continue;
			}
			catch {
				continue;
			}
			if (respectAllowedArea
				&& candidate.playerSettings?.AreaRestrictionInPawnCurrentMap is { } area
				&& !area[target.Position])
				continue;
			return candidate;
		}
		return null;
	}
}