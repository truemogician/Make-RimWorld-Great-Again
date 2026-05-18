using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Utility.Attributes;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.Rimsonable.Patches;

public static class EmergencyJobOverride {
	private const float _OVERRIDE_PRIORITY = 10f;

	private const int _REFRESH_INTERVAL_TICKS = 60;

	private static List<WorkGiverDef>? _emergencyWorkGivers;

	public sealed class DispatchTracker(Map map) : MapComponent(map) {
		private readonly HashSet<int> _dispatchedPawnIds = [];

		public bool IsDispatched(Pawn pawn) => _dispatchedPawnIds.Contains(pawn.thingIDNumber);

		public void Clear() => _dispatchedPawnIds.Clear();

		public void RefreshNow() => Refresh();

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

		// Force a think-tree re-query: pawns on non-emergency jobs never re-evaluate on their own.
		private static void TryTriggerOverride(Pawn pawn) {
			if (!ShouldOverride(pawn))
				return;
			pawn.jobs?.CheckForJobOverride();
		}

		private void Refresh() {
			_dispatchedPawnIds.Clear();
			EnsureEmergencyWorkGiversCached();
			if (_emergencyWorkGivers!.Count == 0)
				return;
			var candidates = EnumerateCandidates().ToList();
			if (candidates.Count == 0)
				return;
			foreach (var wgDef in _emergencyWorkGivers) {
				if (wgDef.Worker is not WorkGiver_Scanner scanner)
					continue;
				DispatchForWorkGiver(scanner, candidates);
			}
		}

		private void DispatchForWorkGiver(WorkGiver_Scanner scanner, List<Pawn> candidates) {
			if (scanner.def.scanThings) {
				// Mirror JobGiver_Work: prefer PotentialWorkThingsGlobal when provided; otherwise fall back to ListerThings
				IEnumerable<Thing>? targets = scanner.PotentialWorkThingsGlobal(candidates[0]);
				if (targets is null) {
					if (scanner.PotentialWorkThingRequest.IsUndefined)
						return;
					targets = map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest);
				}
				foreach (var target in targets) {
					var closest = FindClosestCapable(scanner, target, candidates);
					if (closest is null)
						continue;
					if (_dispatchedPawnIds.Add(closest.thingIDNumber))
						TryTriggerOverride(closest);
				}
				return;
			}
			// NonScanJob-only WGs (e.g. WorkGiver_PatientGoToBedEmergencyTreatment): the calling pawn IS the target.
			foreach (var candidate in candidates) {
				if (_dispatchedPawnIds.Contains(candidate.thingIDNumber) || !PawnCanUseWorkGiver(candidate, scanner))
					continue;
				Job? job = null;
				try {
					job = scanner.NonScanJob(candidate);
				}
				catch {
					// Swallow WorkGiver-internal exceptions; vanilla logs its own.
				}
				if (job is not null && _dispatchedPawnIds.Add(candidate.thingIDNumber))
					TryTriggerOverride(candidate);
			}
		}

		private Pawn? FindClosestCapable(WorkGiver_Scanner scanner, Thing target, List<Pawn> candidates) {
			Pawn? best = null;
			var bestDistSq = int.MaxValue;
			foreach (var candidate in candidates) {
				if (_dispatchedPawnIds.Contains(candidate.thingIDNumber) || !PawnCanUseWorkGiver(candidate, scanner))
					continue;
				try {
					if (!scanner.HasJobOnThing(candidate, target))
						continue;
				}
				catch {
					continue;
				}
				int distSq = (candidate.Position - target.Position).LengthHorizontalSquared;
				if (distSq < bestDistSq) {
					best = candidate;
					bestDistSq = distSq;
				}
			}
			return best;
		}

		private IEnumerable<Pawn> EnumerateCandidates() {
			foreach (var pawn in map.mapPawns.FreeColonistsSpawned) {
				if (pawn is { Drafted: false, Downed: false, InMentalState: false } && !pawn.IsBurning())
					yield return pawn;
			}
		}
	}

	[HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.GetPriority))]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPostfix]
	internal static void JobGiver_Work_GetPriority_Postfix(JobGiver_Work __instance, Pawn pawn, ref float __result) {
		if (!__instance.emergency || __result >= _OVERRIDE_PRIORITY || !ShouldOverride(pawn))
			return;
		__result = _OVERRIDE_PRIORITY;
	}

	[HarmonyPatch(typeof(ThinkNode_ConditionalMustKeepLyingDown), "Satisfied")]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPostfix]
	internal static void MustKeepLyingDown_Satisfied_Postfix(Pawn pawn, ref bool __result) {
		if (!__result || !ShouldOverride(pawn))
			return;
		__result = false;
	}

	[PatchHook(PatchHookTiming.AfterPatch)]
	internal static void AfterPatch() {
		EnsureEmergencyWorkGiversCached();
		if (Find.Maps is null)
			return;
		foreach (var map in Find.Maps)
			map.GetComponent<DispatchTracker>()?.RefreshNow();
	}

	[PatchHook(PatchHookTiming.AfterUnpatch)]
	internal static void AfterUnpatch() {
		if (Find.Maps is null)
			return;
		foreach (var map in Find.Maps)
			map.GetComponent<DispatchTracker>()?.Clear();
	}

	private static bool ShouldOverride(Pawn? pawn) {
		if (pawn?.Map is not { } map)
			return false;
		if (map.GetComponent<DispatchTracker>() is { } tracker && !tracker.IsDispatched(pawn))
			return false;
		return Settings.Default.EmergencyJobInterruptOngoingWork || IsInterruptibleByDefault(pawn);
	}

	private static bool IsInterruptibleByDefault(Pawn pawn) {
		var job = pawn.CurJob;
		return job is null
			|| job.def.joyKind is not null
			|| job.def.casualInterruptible
			|| pawn.GetPosture() != PawnPosture.Standing; // sleeping, resting, or downed
	}

	private static void EnsureEmergencyWorkGiversCached() {
		if (_emergencyWorkGivers != null)
			return;
		_emergencyWorkGivers = DefDatabase<WorkGiverDef>.AllDefsListForReading
			.Where(d => d.emergency)
			.ToList();
	}
}