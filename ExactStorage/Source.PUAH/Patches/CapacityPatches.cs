using System;
using HarmonyLib;
using PickUpAndHaul;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage.PUAH.Patches;

internal static class CapacityPatches {
	[HarmonyPatch(typeof(WorkGiver_HaulToInventory), nameof(WorkGiver_HaulToInventory.CapacityAt))]
	[HarmonyPostfix]
	internal static void WorkGiverHaulToInventory_CapacityAt_Postfix(Thing thing, IntVec3 storeCell, Map map, ref int __result) {
		if (__result <= 0 || storeCell.GetSlotGroup(map)?.Settings is not { } settings)
			return;
		var preferMin = StorageUtility.ShouldPreferForMinimum(settings, thing, storeCell, map);
		var limit = StorageUtility.DestinationCountLimit(settings, thing, preferMin, storeCell, map);
		if (limit != StorageUtility.NO_LIMIT)
			__result = Math.Min(__result, limit);
	}
}