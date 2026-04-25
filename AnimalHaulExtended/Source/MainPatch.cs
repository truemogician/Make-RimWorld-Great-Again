using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.AnimalHaulExtended;

public static class MainPatch {
	internal static TrainableDef HaulDef => field ??= DefDatabase<TrainableDef>.GetNamed("Haul");

	[HarmonyPatch(
		typeof(GenConstruct),
		nameof(GenConstruct.CanConstruct),
		[typeof(Thing), typeof(Pawn), typeof(WorkTypeDef), typeof(bool), typeof(JobDef)]
	)]
	[HarmonyPrefix]
	internal static bool GenConstruct_CanConstruct_WorkType_Prefix(
		Thing t,
		Pawn pawn,
		WorkTypeDef workType,
		bool forced,
		JobDef jobForReservation,
		ref bool __result
	) {
		if (workType != WorkTypeDefOf.Hauling || !IsTrainedPlayerHauler(pawn))
			return true;
		__result = GenConstruct.CanConstruct(t, pawn, false, forced, jobForReservation);
		return false;
	}

	[HarmonyPatch(typeof(JobGiver_Haul), "TryGiveJob")]
	[HarmonyPrefix]
	internal static bool JobGiver_Haul_TryGiveJob_Prefix(Pawn pawn, ref Job? __result) {
		if (!IsTrainedPlayerHauler(pawn))
			return true;
		if (Settings.Default.EnabledWorkGivers.Count == 0)
			return true;
		if (TryGiveExtendedHaulJob(pawn, out var job)) {
			__result = job;
			return false;
		}
		return true;
	}

	private static bool IsTrainedPlayerHauler(Pawn pawn) {
		if (pawn.Map == null)
			return false;
		if (pawn.Faction != Faction.OfPlayer)
			return false;
		if (!pawn.RaceProps.Animal)
			return false;
		return pawn.training?.HasLearned(HaulDef) == true;
	}

	private static bool PawnCanUseWorkGiver(Pawn pawn, WorkGiver giver) {
		if (pawn.WorkTagIsDisabled(giver.def.workTags))
			return false;
		if (giver.ShouldSkip(pawn))
			return false;
		if (giver.MissingRequiredCapacity(pawn) != null)
			return false;
		return !pawn.RaceProps.IsMechanoid || giver.def.canBeDoneByMechs;
	}

	private static bool TryGiveExtendedHaulJob(Pawn pawn, out Job? job) {
		job = null;
		int priorityInType = -999;
		ScanState state = default;

		foreach (var workGiver in Settings.Default.EnabledWorkGivers) {
			if (workGiver.def.priorityInType != priorityInType && state.HasTarget)
				break;
			if (!PawnCanUseWorkGiver(pawn, workGiver))
				continue;
			try {
				if (workGiver.NonScanJob(pawn) is { } nonScanJob) {
					nonScanJob.workGiverDef = workGiver.def;
					job = nonScanJob;
					return true;
				}

				if (workGiver is WorkGiver_Scanner scanner) {
					if (scanner.def.scanThings)
						ScanThings(pawn, scanner, ref state);
					if (scanner.def.scanCells)
						ScanCells(pawn, scanner, ref state);
				}
			}
			catch (Exception ex) {
				Helper.Logger.Error($"{pawn} threw exception in WorkGiver {workGiver.def.defName}: {ex}");
			}
			if (state.HasTarget) {
				job = state.Target.HasThing
					? state.Scanner!.JobOnThing(pawn, state.Target.Thing)
					: state.Scanner!.JobOnCell(pawn, state.Target.Cell);
				if (job != null) {
					job.workGiverDef = state.Scanner.def;
					return true;
				}
				Helper.Logger.Error(
					$"{state.Scanner} provided target {state.Target} but yielded no actual job for pawn {pawn}. The CanGiveJob and JobOnX methods may not be synchronized.",
					true
				);
			}
			priorityInType = workGiver.def.priorityInType;
		}

		return false;
	}

	private static void ScanThings(Pawn pawn, WorkGiver_Scanner scanner, ref ScanState state) {
		var globalThings = scanner.PotentialWorkThingsGlobal(pawn);

		Thing? thing;
		if (scanner.Prioritized) {
			var searchSet = globalThings ?? pawn.Map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest);
			thing = scanner.AllowUnreachable
				? GenClosest.ClosestThing_Global(pawn.Position, searchSet, 99999f, Validator, t => scanner.GetPriority(pawn, t))
				: GenClosest.ClosestThing_Global_Reachable(
					pawn.Position,
					pawn.Map,
					searchSet,
					scanner.PathEndMode,
					TraverseParms.For(pawn, scanner.MaxPathDanger(pawn)),
					9999f,
					Validator,
					t => scanner.GetPriority(pawn, t)
				);
		}
		else if (scanner.AllowUnreachable) {
			var searchSet = globalThings ?? pawn.Map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest);
			thing = GenClosest.ClosestThing_Global(pawn.Position, searchSet, 99999f, Validator);
		}
		else {
			thing = GenClosest.ClosestThingReachable(
				pawn.Position,
				pawn.Map,
				scanner.PotentialWorkThingRequest,
				scanner.PathEndMode,
				TraverseParms.For(pawn, scanner.MaxPathDanger(pawn)),
				9999f,
				Validator,
				globalThings,
				0,
				scanner.MaxRegionsToScanBeforeGlobalSearch,
				globalThings != null
			);
		}

		if (thing == null)
			return;
		state.Target = thing;
		state.Scanner = scanner;
		state.ClosestDistSquared = (thing.Position - pawn.Position).LengthHorizontalSquared;
		state.BestPriority = scanner.Prioritized ? scanner.GetPriority(pawn, thing) : float.MinValue;
		return;

		bool Validator(Thing t) => !t.IsForbidden(pawn) && scanner.HasJobOnThing(pawn, t);
	}

	private static void ScanCells(Pawn pawn, WorkGiver_Scanner scanner, ref ScanState state) {
		bool prioritized = scanner.Prioritized;
		bool allowUnreachable = scanner.AllowUnreachable;
		var maxPathDanger = scanner.MaxPathDanger(pawn);

		foreach (var cell in scanner.PotentialWorkCellsGlobal(pawn)) {
			float distSquared = (cell - pawn.Position).LengthHorizontalSquared;
			var cellPriority = 0f;
			bool isCandidate;
			if (prioritized) {
				if (cell.IsForbidden(pawn) || !scanner.HasJobOnCell(pawn, cell))
					continue;
				if (!allowUnreachable && !pawn.CanReach(cell, scanner.PathEndMode, maxPathDanger))
					continue;
				cellPriority = scanner.GetPriority(pawn, cell);
				// fuzzy compare to avoid float-equality flicker between ties
				isCandidate = cellPriority > state.BestPriority
					|| (Math.Abs(cellPriority - state.BestPriority) < 0.001f && distSquared < state.ClosestDistSquared);
			}
			else {
				if (distSquared >= state.ClosestDistSquared)
					continue;
				if (cell.IsForbidden(pawn) || !scanner.HasJobOnCell(pawn, cell))
					continue;
				if (!allowUnreachable && !pawn.CanReach(cell, scanner.PathEndMode, maxPathDanger))
					continue;
				isCandidate = true;
			}
			if (!isCandidate)
				continue;
			state.Target = new TargetInfo(cell, pawn.Map);
			state.Scanner = scanner;
			state.ClosestDistSquared = distSquared;
			state.BestPriority = cellPriority;
		}
	}

	private struct ScanState() {
		public TargetInfo Target = TargetInfo.Invalid;

		public WorkGiver_Scanner? Scanner = null;

		public float ClosestDistSquared = 99999f;

		public float BestPriority = float.MinValue;

		public readonly bool HasTarget => Target.IsValid && Scanner != null;
	}
}