using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Utility.Attributes;
using Verse;

namespace TrueMogician.RimWorld.RemedialAlgorithms.Patches;

[HarmonyPatch]
public static class FoodSearchOptimizationPatches {
	private static readonly Dictionary<FoodSearchCacheKey, int> _negativeCache = [];

	public readonly record struct FoodSearchCacheKey(
		int MapId,
		int RegionId,
		ThingRequestGroup RequestGroup,
		ushort EaterDefHash,
		FoodTypeFlags FoodType,
		bool Desperate,
		bool AllowPlant,
		bool ForceScanWholeMap,
		FoodPreferability MinPrefOverride
	);

	[HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.BestFoodSourceOnMap))]
	[HarmonyPrefix]
	[HarmonyPriority(Priority.Last)]
	public static bool BestFoodSourceOnMap_Prefix(
		Pawn? getter,
		Pawn? eater,
		bool desperate,
		bool allowPlant,
		bool forceScanWholeMap,
		FoodPreferability minPrefOverride,
		ref ThingDef? foodDef,
		ref Thing? __result,
		out OptimizationState __state
	) {
		__state = default;
		if (!TryMakeKey(getter, eater, desperate, allowPlant, forceScanWholeMap, minPrefOverride, out var key))
			return true;
		__state.Key = key;
		if (!_negativeCache.TryGetValue(key, out int expireTick))
			return true;
		if (expireTick < Find.TickManager.TicksGame) {
			_negativeCache.Remove(key);
			return true;
		}
		__state.CacheHit = true;
		foodDef = null;
		__result = null;
		return false;
	}

	[HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.BestFoodSourceOnMap))]
	[HarmonyPostfix]
	public static void BestFoodSourceOnMap_Postfix(bool desperate, Thing? __result, ref OptimizationState __state) {
		if (__state.Key is not { } key || __state.CacheHit || __result != null)
			return;
		_negativeCache[key] = Find.TickManager.TicksGame
			+ (desperate ? Settings.Default.WildAnimalFoodSearchStarvingCacheTtl : Settings.Default.WildAnimalFoodSearchCacheTtl);
		PruneCache();
	}

	[PatchHook(PatchHookTiming.AfterUnpatch)]
	public static void ClearOnUnpatch() => _negativeCache.Clear();

	private static bool TryMakeKey(
		Pawn? getter,
		Pawn? eater,
		bool desperate,
		bool allowPlant,
		bool forceScanWholeMap,
		FoodPreferability minPrefOverride,
		out FoodSearchCacheKey key
	) {
		key = default;
		if (getter == null
			|| eater == null
			|| !ReferenceEquals(getter, eater)
			|| getter is not { Spawned: true, MapHeld: not null, IsAnimal: true, Faction: null })
			return false;
		var map = getter.MapHeld;
		var region = getter.PositionHeld.GetRegion(map);
		if (region == null)
			return false;
		bool requestIncludesPlants = (eater.RaceProps.foodType & (FoodTypeFlags.Plant | FoodTypeFlags.Tree)) != 0 && allowPlant;
		var requestGroup = requestIncludesPlants ? ThingRequestGroup.FoodSource : ThingRequestGroup.FoodSourceNotPlantOrTree;
		key = new FoodSearchCacheKey(
			map.uniqueID,
			region.id,
			requestGroup,
			eater.def.shortHash,
			eater.RaceProps.foodType,
			desperate,
			allowPlant,
			forceScanWholeMap,
			minPrefOverride
		);
		return true;
	}

	private static void PruneCache() {
		if (_negativeCache.Count < 4096)
			return;
		int ticksGame = Find.TickManager.TicksGame;
		foreach (var key in _negativeCache.Keys.ToList()) {
			if (_negativeCache[key] < ticksGame)
				_negativeCache.Remove(key);
		}
		if (_negativeCache.Count > 4096)
			_negativeCache.Clear();
	}

	public struct OptimizationState {
		public FoodSearchCacheKey? Key { get; set; }

		public bool CacheHit { get; set; }
	}
}