using System;
using HarmonyLib;
using RimWorld;
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
		if (!MapHasActiveQuotaFor(map, t.InnerDef))
			return;
		if (TryFindPreferredUnderMinCell(t, carrier, map, currentPriority, faction, out var preferredCell, out var preferredDestination)) {
			foundCell = preferredCell;
			haulDestination = preferredDestination;
			__result = true;
		}
		else if (__result && foundCell.IsValid && !foundCell.CanReceiveAt(map, t)) {
			if (TryFindAllowedCell(t, carrier, map, currentPriority, faction, out var allowedCell, out var allowedDestination)) {
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
		if (__result is null || storeCell.GetSlotGroup(p.Map) is not { } slotGroup)
			return;
		var settings = slotGroup.Settings;
		if (t.IsCurrentStorageScope(settings, slotGroup.parent)) {
			__result = null;
			return;
		}
		uint limit = NO_LIMIT;
		if (Manager.TryGetProfile(settings, out var profile) && profile.Enabled) {
			bool preferMin = settings.ShouldPreferForMinimum(t, storeCell, p.Map);
			limit = settings.DestinationCountLimit(t, preferMin, storeCell, p.Map);
		}
		uint sourceLimit = t.SourceCountLimit(storeCell, p.Map);
		if (sourceLimit != NO_LIMIT)
			limit = Math.Min(limit, sourceLimit);
		if (limit != NO_LIMIT) {
			int cappedLimit = limit > int.MaxValue ? int.MaxValue : (int)limit;
			if (cappedLimit < __result.count) {
				__result.count = cappedLimit;
				__result.haulOpportunisticDuplicates = false;
			}
		}
		if (__result.count <= 0)
			__result = null;
	}

	[HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.HaulToStorageJob))]
	[HarmonyPostfix]
	internal static void HaulAIUtility_HaulToStorageJob_Postfix(Thing t, ref Job? __result) {
		if (__result is null)
			return;
		uint limit = t.SourceExcessLimit();
		var storeCell = __result.GetTarget(TargetIndex.B).Cell;
		if (storeCell.IsValid && t.MapHeld is { } map) {
			uint sourceLimit = t.SourceCountLimit(storeCell, map);
			if (sourceLimit != NO_LIMIT)
				limit = Math.Min(limit, sourceLimit);
		}
		if (limit != NO_LIMIT)
			__result.count = Math.Min(__result.count, limit > int.MaxValue ? int.MaxValue : (int)limit);
		if (__result.count <= 0)
			__result = null;
	}

	[HarmonyPatch(typeof(Zone_Stockpile), nameof(Zone_Stockpile.Notify_ReceivedThing))]
	[HarmonyPostfix]
	internal static void ZoneStockpile_NotifyReceivedThing_Postfix(Zone_Stockpile __instance) => __instance.GetStoreSettings().NotifyChanged();

	[HarmonyPatch(typeof(Zone_Stockpile), nameof(Zone_Stockpile.Notify_LostThing))]
	[HarmonyPostfix]
	internal static void ZoneStockpile_NotifyLostThing_Postfix(Zone_Stockpile __instance) => __instance.GetStoreSettings().NotifyChanged();

	[HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.Notify_ReceivedThing))]
	[HarmonyPostfix]
	internal static void BuildingStorage_NotifyReceivedThing_Postfix(Building_Storage __instance) => __instance.GetStoreSettings().NotifyChanged();

	[HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.Notify_LostThing))]
	[HarmonyPostfix]
	internal static void BuildingStorage_NotifyLostThing_Postfix(Building_Storage __instance) => __instance.GetStoreSettings().NotifyChanged();
}