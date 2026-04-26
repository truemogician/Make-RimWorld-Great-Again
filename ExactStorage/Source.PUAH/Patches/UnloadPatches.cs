using System;
using HarmonyLib;
using PickUpAndHaul;
using RimWorld;
using Verse.AI;

namespace TrueMogician.RimWorld.ExactStorage.PUAH.Patches;

internal static class UnloadPatches {
	[HarmonyPatch(typeof(JobDriver_UnloadYourHauledInventory), "FindTargetOrDrop")]
	[HarmonyPostfix]
	internal static void JobDriverUnloadYourHauledInventory_FindTargetOrDrop_Postfix(
		JobDriver_UnloadYourHauledInventory __instance,
		ref Toil __result
	) {
		var action = __result.initAction;
		__result.initAction = () => {
			action?.Invoke();
			CapCountToDrop(__instance);
		};
	}

	private static void CapCountToDrop(JobDriver_UnloadYourHauledInventory driver) {
		var pawn = driver.pawn;
		var job = driver.job;
		if (pawn?.Map is not { } map || job is null)
			return;
		if (pawn.CurJob != job)
			return;
		var target = job.GetTarget(TargetIndex.B);
		if (target.HasThing || !target.Cell.IsValid || target.Cell.GetSlotGroup(map)?.Settings is not { } settings)
			return;
		var thing = job.GetTarget(TargetIndex.A).Thing;
		if (thing is null)
			return;
		ref var countToDrop = ref Access.CountToDrop(driver);
		if (countToDrop <= 0)
			return;
		var preferMin = StorageUtility.ShouldPreferForMinimum(settings, thing, target.Cell, map, job);
		var limit = StorageUtility.DestinationCountLimit(settings, thing, preferMin, target.Cell, map, job);
		if (limit == StorageUtility.NO_LIMIT || limit >= countToDrop)
			return;
		if (limit <= 0) {
			driver.EndJobWith(JobCondition.Incompletable);
			return;
		}
		countToDrop = Math.Min(countToDrop, limit);
		job.count = countToDrop;
	}
}