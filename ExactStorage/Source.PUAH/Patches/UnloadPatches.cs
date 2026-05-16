using System;
using HarmonyLib;
using PickUpAndHaul;
using RimWorld;
using TrueMogician.RimWorld.Utility.Diagnostics;
using Verse.AI;

namespace TrueMogician.RimWorld.ExactStorage.PUAH.Patches;

internal static class UnloadPatches {
	[HarmonyPatch(typeof(JobDriver_UnloadYourHauledInventory), "FindTargetOrDrop")]
	[HarmonyPostfix]
	internal static void JobDriver_UnloadYourHauledInventory_FindTargetOrDrop_Postfix(
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
		if (pawn?.Map is not { } map || job is null) {
			Diagnostic.Record("PuahUnload", "no_map_or_job", pawn, minimum: Verbosity.Full);
			return;
		}
		if (pawn.CurJob != job) {
			Diagnostic.Record("PuahUnload", "wrong_job", pawn, minimum: Verbosity.Full);
			return;
		}
		var target = job.GetTarget(TargetIndex.B);
		if (target.HasThing || !target.Cell.IsValid || target.Cell.GetSlotGroup(map)?.Settings is not { } settings) {
			Diagnostic.Record(
				"PuahUnload",
				"invalid_target",
				pawn,
				$"hasThing={target.HasThing}\tcellValid={target.Cell.IsValid}\tcell={(target.Cell.IsValid ? target.Cell.ToString() : "invalid")}",
				Verbosity.Full
			);
			return;
		}
		var thing = job.GetTarget(TargetIndex.A).Thing;
		if (thing is null) {
			Diagnostic.Record("PuahUnload", "no_thing", pawn, null, target.Cell, minimum: Verbosity.Full);
			return;
		}
		ref int countToDrop = ref Access.CountToDrop(driver);
		int before = countToDrop;
		if (countToDrop <= 0) {
			Diagnostic.Record("PuahUnload", "zero_count", pawn, thing, target.Cell, $"before={before}", Verbosity.Full);
			return;
		}
		bool preferMin = settings.ShouldPreferForMinimum(thing, target.Cell, map, job);
		uint limit = settings.DestinationCountLimit(thing, preferMin, target.Cell, map, job);
		if (limit == StorageUtility.NO_LIMIT || limit >= (uint)countToDrop)
			return;
		if (limit == 0u) {
			Diagnostic.Record("PuahUnload", "incompletable", pawn, thing, target.Cell, $"before={before}");
			driver.EndJobWith(JobCondition.Incompletable);
			return;
		}
		countToDrop = Math.Min(countToDrop, limit > int.MaxValue ? int.MaxValue : (int)limit);
		job.count = countToDrop;
		Diagnostic.Record(
			"PuahUnload",
			"capped",
			pawn,
			thing,
			target.Cell,
			$"before={before}\tafter={countToDrop}\tpreferMin={preferMin}\tlimit={limit}"
		);
	}
}