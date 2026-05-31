using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.RemedialAlgorithms.Components;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;
using Verse;

namespace TrueMogician.RimWorld.RemedialAlgorithms.Patches;

[HarmonyPatch]
public static class TendTargetCachePatches {
	private static readonly AccessTools.FieldRef<MapPawns, Map> _mapFieldRef =
		AccessTools.FieldRefAccess<MapPawns, Map>("map");

	[PatchHook(PatchHookTiming.AfterPatch)]
	public static void AfterPatch() {
		TendStatusCache.Activate();
		if (Find.Maps is null)
			return;
		foreach (var map in Find.Maps) {
			var cache = CachedMapComponent<TendTargetCache>.Get(map);
			cache?.Subscribe();
			cache?.Rebuild();
		}
	}

	[PatchHook(PatchHookTiming.AfterUnpatch)]
	public static void AfterUnpatch() {
		if (Find.Maps != null) {
			foreach (var map in Find.Maps)
				CachedMapComponent<TendTargetCache>.Get(map)?.Unsubscribe();
		}
		TendStatusCache.Deactivate();
	}

	// #region Hediff / map-pawn signal forwarding

	[HarmonyPatch(typeof(HediffSet), nameof(HediffSet.DirtyCache))]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPostfix]
	public static void HediffSet_DirtyCache_Postfix(HediffSet __instance) => HediffDirtyHub.OnHediffsDirtied(__instance.pawn);

	[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.RegisterPawn))]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPostfix]
	public static void MapPawns_RegisterPawn_Postfix(MapPawns __instance, Pawn p) => HediffDirtyHub.OnPawnSpawned(p, GetMap(__instance));

	[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.DeRegisterPawn))]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPostfix]
	public static void MapPawns_DeRegisterPawn_Postfix(MapPawns __instance, Pawn p) => HediffDirtyHub.OnPawnDespawned(p, GetMap(__instance));

	// #endregion

	// #region Replace SpawnedPawnsWithAnyHediff with the cached list

	[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.SpawnedPawnsWithAnyHediff), MethodType.Getter)]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPrefix]
	public static bool SpawnedPawnsWithAnyHediff_Getter_Prefix(MapPawns __instance, ref List<Pawn> __result) {
		if (GetMap(__instance) is not { } map)
			return true;
		if (CachedMapComponent<TendTargetCache>.Get(map) is not { } cache)
			return true;
		__result = cache.WithAnyHediff;
		return false;
	}

	// #endregion

	// #region Memoize ShouldBeTendedNowByPlayer / …Urgent per (pawn, tick)

	[HarmonyPatch(typeof(HealthAIUtility), nameof(HealthAIUtility.ShouldBeTendedNowByPlayer))]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPrefix]
	public static bool ShouldBeTendedNowByPlayer_Prefix(Pawn? pawn, ref bool __result) {
		if (pawn is null)
			return true;
		if (!TendStatusCache.TryGetNeedsTending(pawn, out bool cached))
			return true;
		__result = cached;
		return false;
	}

	[HarmonyPatch(typeof(HealthAIUtility), nameof(HealthAIUtility.ShouldBeTendedNowByPlayer))]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPostfix]
	public static void ShouldBeTendedNowByPlayer_Postfix(Pawn? pawn, bool __result) {
		if (pawn != null)
			TendStatusCache.StoreNeedsTending(pawn, __result);
	}

	[HarmonyPatch(typeof(HealthAIUtility), nameof(HealthAIUtility.ShouldBeTendedNowByPlayerUrgent))]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPrefix]
	public static bool ShouldBeTendedNowByPlayerUrgent_Prefix(Pawn? pawn, ref bool __result) {
		if (pawn is null)
			return true;
		if (!TendStatusCache.TryGetNeedsUrgentTending(pawn, out bool cached))
			return true;
		__result = cached;
		return false;
	}

	[HarmonyPatch(typeof(HealthAIUtility), nameof(HealthAIUtility.ShouldBeTendedNowByPlayerUrgent))]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPostfix]
	public static void ShouldBeTendedNowByPlayerUrgent_Postfix(Pawn? pawn, bool __result) {
		if (pawn != null)
			TendStatusCache.StoreNeedsUrgentTending(pawn, __result);
	}

	// #endregion

	// #region Narrow target lists for Tend WorkGivers

	[HarmonyPatch(typeof(WorkGiver_Tend), nameof(WorkGiver_Tend.PotentialWorkThingsGlobal))]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPrefix]
	public static bool WorkGiver_Tend_PotentialWorkThingsGlobal_Prefix(WorkGiver_Tend __instance, Pawn? pawn, ref IEnumerable<Thing> __result) {
		if (pawn?.Map is not { } map)
			return true;
		if (CachedMapComponent<TendTargetCache>.Get(map) is not { } cache)
			return true;
		__result = __instance is WorkGiver_TendOtherUrgent ? cache.NeedingUrgentTending : cache.NeedingTending;
		return false;
	}

	// #endregion

	private static Map? GetMap(MapPawns mapPawns) => _mapFieldRef(mapPawns);
}