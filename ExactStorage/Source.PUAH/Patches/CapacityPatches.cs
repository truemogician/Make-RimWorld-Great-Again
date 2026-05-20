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
		if (__result <= 0)
			return;
		if (storeCell.GetSlotGroup(map)?.Settings is not { } settings)
			return;
		bool preferMin = settings.ShouldPreferForMinimum(thing, storeCell, map);
		uint destLimit = settings.DestinationCountLimit(thing, preferMin, storeCell, map);
		uint sourceLimit = thing.SourceCountLimit(storeCell, map);
		uint limit = destLimit;
		if (sourceLimit != StorageUtility.NO_LIMIT)
			limit = Math.Min(limit, sourceLimit);
		if (limit != StorageUtility.NO_LIMIT)
			__result = Math.Min(__result, limit > int.MaxValue ? int.MaxValue : (int)limit);
	}
}