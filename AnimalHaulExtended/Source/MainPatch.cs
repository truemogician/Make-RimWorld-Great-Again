using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.AnimalHaulExtended;

public static class AnimalHaulExtension {
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
		if (!IsTrainedPlayerHauler(pawn) || workType != WorkTypeDefOf.Hauling)
			return true;
		__result = GenConstruct.CanConstruct(t, pawn, workType == WorkTypeDefOf.Construction, forced, jobForReservation);
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

	private static Thing? FindBestThingTarget(Pawn pawn, WorkGiver_Scanner scanner) {
		IEnumerable<Thing>? globalThings = scanner.PotentialWorkThingsGlobal(pawn);
		if (scanner.Prioritized) {
			var searchSet = globalThings ?? pawn.Map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest);
			return scanner.AllowUnreachable
				? GenClosest.ClosestThing_Global(pawn.Position, searchSet, 99999f, Validator, thing => scanner.GetPriority(pawn, thing))
				: GenClosest.ClosestThing_Global_Reachable(
					pawn.Position,
					pawn.Map,
					searchSet,
					scanner.PathEndMode,
					TraverseParms.For(pawn, scanner.MaxPathDanger(pawn)),
					9999f,
					Validator,
					thing => scanner.GetPriority(pawn, thing)
				);
		}

		if (scanner.AllowUnreachable) {
			var searchSet = globalThings ?? pawn.Map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest);
			return GenClosest.ClosestThing_Global(pawn.Position, searchSet, 99999f, Validator);
		}

		return GenClosest.ClosestThingReachable(
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

		bool Validator(Thing t) => !t.IsForbidden(pawn) && scanner.HasJobOnThing(pawn, t);
	}

	private static bool TryFindBestCellTarget(Pawn pawn, WorkGiver_Scanner scanner, out IntVec3 bestCell) {
		bestCell = IntVec3.Invalid;
		var closestDistSquared = 99999f;
		var bestPriority = float.MinValue;
		bool prioritized = scanner.Prioritized;
		bool allowUnreachable = scanner.AllowUnreachable;
		var maxPathDanger = scanner.MaxPathDanger(pawn);

		foreach (var cell in scanner.PotentialWorkCellsGlobal(pawn)) {
			var isCandidate = false;
			float distSquared = (cell - pawn.Position).LengthHorizontalSquared;
			var cellPriority = 0f;

			if (prioritized) {
				if (!cell.IsForbidden(pawn) && scanner.HasJobOnCell(pawn, cell)) {
					if (!allowUnreachable && !pawn.CanReach(cell, scanner.PathEndMode, maxPathDanger))
						continue;
					cellPriority = scanner.GetPriority(pawn, cell);
					if (cellPriority > bestPriority || (Math.Abs(cellPriority - bestPriority) < 0.001f && distSquared < closestDistSquared))
						isCandidate = true;
				}
			}
			else if (distSquared < closestDistSquared && !cell.IsForbidden(pawn) && scanner.HasJobOnCell(pawn, cell)) {
				if (!allowUnreachable && !pawn.CanReach(cell, scanner.PathEndMode, maxPathDanger))
					continue;
				isCandidate = true;
			}

			if (!isCandidate)
				continue;

			bestCell = cell;
			closestDistSquared = distSquared;
			bestPriority = cellPriority;
		}

		return bestCell.IsValid;
	}

	private static bool TryGiveExtendedHaulJob(Pawn pawn, out Job? job) {
		job = null;
		int priorityInType = -999;
		var bestTarget = TargetInfo.Invalid;
		WorkGiver_Scanner? scannerWhoProvidedTarget = null;

		foreach (var workGiver in Settings.Default.EnabledWorkGivers) {
			if (workGiver.def.priorityInType != priorityInType && bestTarget.IsValid)
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
					if (scanner.def.scanThings && FindBestThingTarget(pawn, scanner) is { } thingTarget) {
						bestTarget = thingTarget;
						scannerWhoProvidedTarget = scanner;
					}

					if (scanner.def.scanCells && TryFindBestCellTarget(pawn, scanner, out var cellTarget)) {
						bestTarget = new TargetInfo(cellTarget, pawn.Map);
						scannerWhoProvidedTarget = scanner;
					}
				}
			}
			catch (Exception ex) {
				Helper.Logger.Error($"{pawn} threw exception in WorkGiver {workGiver.def.defName}: {ex}");
			}

			if (bestTarget.IsValid && scannerWhoProvidedTarget != null) {
				job = bestTarget.HasThing
					? scannerWhoProvidedTarget.JobOnThing(pawn, bestTarget.Thing)
					: scannerWhoProvidedTarget.JobOnCell(pawn, bestTarget.Cell);
				if (job != null) {
					job.workGiverDef = scannerWhoProvidedTarget.def;
					return true;
				}
				Helper.Logger.Error(
					$"{scannerWhoProvidedTarget} provided target {bestTarget} but yielded no actual job for pawn {pawn}. The CanGiveJob and JobOnX methods may not be synchronized.",
					true
				);
			}

			priorityInType = workGiver.def.priorityInType;
		}

		return false;
	}
}