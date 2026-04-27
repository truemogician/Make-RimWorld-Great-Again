using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.ExactStorage.Patches;

using static StorageUtility;

internal static class StorageBehaviorPatches {
	[HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterStorageFor))]
	[HarmonyPostfix]
	internal static void StoreUtility_TryFindBestBetterStorageFor_Postfix(
		Thing t,
		Pawn carrier,
		Map map,
		StoragePriority currentPriority,
		Faction faction,
		ref IntVec3 foundCell,
		ref IHaulDestination haulDestination,
		ref bool __result
	) {
		var evaluation = new StorageEvaluationCache();
		if (TryFindPreferredUnderMinCell(
				t,
				carrier,
				map,
				currentPriority,
				faction,
				evaluation,
				out var preferredCell,
				out var preferredDestination
			)) {
			foundCell = preferredCell;
			haulDestination = preferredDestination;
			__result = true;
		}
		else if (__result && foundCell.IsValid && !CanReceiveAt(foundCell, map, t, evaluation)) {
			if (TryFindAllowedCell(t, carrier, map, currentPriority, faction, evaluation, out var allowedCell, out var allowedDestination)) {
				foundCell = allowedCell;
				haulDestination = allowedDestination;
				__result = true;
			}
			else
				__result = false;
		}
	}

	[HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.HaulToCellStorageJob))]
	[HarmonyPostfix]
	internal static void HaulAIUtility_HaulToCellStorageJob_Postfix(Pawn p, Thing t, IntVec3 storeCell, ref Job? __result) {
		if (__result is null || storeCell.GetSlotGroup(p.Map)?.Settings is not { } settings)
			return;
		if (!Manager.TryGetProfile(settings, out var profile) || !profile.Enabled)
			return;
		var evaluation = new StorageEvaluationCache();
		var preferMin = ShouldPreferForMinimum(settings, t, storeCell, p.Map, evaluation);
		var limit = DestinationCountLimit(settings, t, preferMin, storeCell, p.Map, evaluation);
		if (limit != NO_LIMIT && limit < __result.count) {
			__result.count = Mathf.Min(__result.count, limit);
			__result.haulOpportunisticDuplicates = false;
		}
		if (__result.count <= 0)
			__result = null;
	}

	[HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.HaulToStorageJob))]
	[HarmonyPostfix]
	internal static void HaulAIUtility_HaulToStorageJob_Postfix(Thing t, ref Job? __result) {
		if (__result is null)
			return;
		var limit = SourceExcessLimit(t);
		if (limit != NO_LIMIT)
			__result.count = Mathf.Min(__result.count, limit);
		if (__result.count <= 0)
			__result = null;
	}

	[HarmonyPatch(typeof(Zone_Stockpile), nameof(Zone_Stockpile.Notify_ReceivedThing))]
	[HarmonyPostfix]
	internal static void ZoneStockpile_NotifyReceivedThing_Postfix(Zone_Stockpile __instance) => NotifyChanged(__instance.GetStoreSettings());

	[HarmonyPatch(typeof(Zone_Stockpile), nameof(Zone_Stockpile.Notify_LostThing))]
	[HarmonyPostfix]
	internal static void ZoneStockpile_NotifyLostThing_Postfix(Zone_Stockpile __instance) => NotifyChanged(__instance.GetStoreSettings());

	[HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.Notify_ReceivedThing))]
	[HarmonyPostfix]
	internal static void BuildingStorage_NotifyReceivedThing_Postfix(Building_Storage __instance) => NotifyChanged(__instance.GetStoreSettings());

	[HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.Notify_LostThing))]
	[HarmonyPostfix]
	internal static void BuildingStorage_NotifyLostThing_Postfix(Building_Storage __instance) => NotifyChanged(__instance.GetStoreSettings());
}