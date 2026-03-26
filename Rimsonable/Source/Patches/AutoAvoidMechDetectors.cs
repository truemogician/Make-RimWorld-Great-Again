using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Rimsonable.Static;
using TrueMogician.RimWorld.Utility.Attributes;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.Rimsonable.Patches;

public static class AutoAvoidMechDetectors {
	public const int AVOID_COST = 800;

	public const float RADIUS_MARGIN = 1f;

	private static readonly List<ThingDef> _detectorDefs = [];

	private static bool _detectorDefsInitialized;

	private static ConditionalWeakTable<Map, DetectorCoverage> _coverageGrids = new();

	static AutoAvoidMechDetectors() {
		LongEventHandler.ExecuteWhenFinished(EnsureDetectorDefsInitialized);
	}

	[HarmonyPatch(typeof(PathGrid), nameof(PathGrid.CalculatedCostAt))]
	[HarmonyPostfix]
	private static void PathGrid_CalculatedCostAt_Postfix(
		PathGrid __instance,
		IntVec3 c,
		bool perceivedStatic,
		ref int __result
	) {
		if (!perceivedStatic || __result >= 10000)
			return;
		var grid = GetOrBuildCoverageGrid(__instance.map);
		if (grid.Grid[c] > 0)
			__result += AVOID_COST;
	}

	[HarmonyPatch(typeof(Map), nameof(Map.FinalizeInit))]
	[HarmonyPostfix]
	private static void Map_FinalizeInit_Postfix(Map __instance) => RebuildCoverageGrid(__instance);

	[HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.SpawnSetup))]
	[HarmonyPostfix]
	private static void ThingWithComps_SpawnSetup_Postfix(ThingWithComps __instance) {
		var comp = __instance.GetComp<CompSendSignalOnMotion>();
		if (comp is not { Sent: false } || !__instance.Spawned)
			return;
		ActivateDetector(__instance.Map, __instance, comp.Props.radius);
	}

	[HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.DeSpawn))]
	[HarmonyPrefix]
	private static void ThingWithComps_DeSpawn_Prefix(ThingWithComps __instance) {
		var comp = __instance.GetComp<CompSendSignalOnMotion>();
		if (comp is not { Sent: false } || !__instance.Spawned)
			return;
		DeactivateDetector(__instance.Map, __instance, comp.Props.radius);
	}

	// #region Trigger / Expire
	[HarmonyPatch(typeof(CompSendSignalOnMotion), "Trigger")]
	[HarmonyPrefix]
	private static void CompSendSignalOnMotion_Trigger_Prefix(CompSendSignalOnMotion __instance, out bool __state)
		=> __state = IsActive(__instance);

	[HarmonyPatch(typeof(CompSendSignalOnMotion), "Trigger")]
	[HarmonyPostfix]
	private static void CompSendSignalOnMotion_Trigger_Postfix(CompSendSignalOnMotion __instance, bool __state)
		=> SyncDetectorCoverage(__instance, __state);

	[HarmonyPatch(typeof(CompSendSignalOnMotion), nameof(CompSendSignalOnMotion.Notify_SignalReceived))]
	[HarmonyPrefix]
	private static void CompSendSignalOnMotion_Notify_SignalReceived_Prefix(CompSendSignalOnMotion __instance, out bool __state)
		=> __state = IsActive(__instance);

	[HarmonyPatch(typeof(CompSendSignalOnMotion), nameof(CompSendSignalOnMotion.Notify_SignalReceived))]
	[HarmonyPostfix]
	private static void CompSendSignalOnMotion_Notify_SignalReceived_Postfix(CompSendSignalOnMotion __instance, bool __state)
		=> SyncDetectorCoverage(__instance, __state);

	[HarmonyPatch(typeof(CompSendSignalOnMotion), nameof(CompSendSignalOnMotion.Expire))]
	[HarmonyPrefix]
	private static void CompSendSignalOnMotion_Expire_Prefix(CompSendSignalOnMotion __instance, out bool __state)
		=> __state = IsActive(__instance);

	[HarmonyPatch(typeof(CompSendSignalOnMotion), nameof(CompSendSignalOnMotion.Expire))]
	[HarmonyPostfix]
	private static void CompSendSignalOnMotion_Expire_Postfix(CompSendSignalOnMotion __instance, bool __state)
		=> SyncDetectorCoverage(__instance, __state);
	// #endregion

	[PatchHook(PatchHookTiming.AfterPatch)]
	private static void AfterPatch() {
		EnsureDetectorDefsInitialized();
		if (Find.Maps == null)
			return;
		foreach (var map in Find.Maps)
			RebuildCoverageGrid(map);
	}

	[PatchHook(PatchHookTiming.AfterUnpatch)]
	private static void AfterUnpatch() {
		if (Find.Maps != null) {
			foreach (var map in Find.Maps) {
				if (_coverageGrids.TryGetValue(map, out var grid))
					grid.RefreshCoveredCells();
			}
		}
		_coverageGrids = new();
	}

	private static bool IsActive(CompSendSignalOnMotion comp) => comp is { parent.Spawned: true, Sent: false };

	private static void SyncDetectorCoverage(CompSendSignalOnMotion comp, bool wasActive) {
		bool isActive = IsActive(comp);
		if (wasActive == isActive || comp.parent is not { Spawned: true } parent)
			return;
		if (isActive)
			ActivateDetector(parent.Map, parent, comp.Props.radius);
		else
			DeactivateDetector(parent.Map, parent, comp.Props.radius);
	}

	private static void ActivateDetector(Map map, ThingWithComps detector, float detectionRadius) {
		GetOrCreateEmptyCoverageGrid(map).SetDetectorCoverage(detector, detectionRadius, true, true);
	}

	private static void DeactivateDetector(Map map, ThingWithComps detector, float detectionRadius) {
		if (!_coverageGrids.TryGetValue(map, out var grid))
			return;
		grid.SetDetectorCoverage(detector, detectionRadius, false, true);
	}

	private static void EnsureDetectorDefsInitialized() {
		if (_detectorDefsInitialized)
			return;
		_detectorDefsInitialized = true;
		foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading) {
			if (def.HasComp<CompSendSignalOnMotion>())
				_detectorDefs.Add(def);
		}
	}

	private static DetectorCoverage GetOrCreateEmptyCoverageGrid(Map map) {
		if (_coverageGrids.TryGetValue(map, out var grid))
			return grid;
		grid = new DetectorCoverage(map);
		_coverageGrids.Add(map, grid);
		return grid;
	}

	private static DetectorCoverage GetOrBuildCoverageGrid(Map map) {
		if (_coverageGrids.TryGetValue(map, out var grid))
			return grid;
		grid = GetOrCreateEmptyCoverageGrid(map);
		grid.Populate(EnumerateActiveDetectors(map), false);
		return grid;
	}

	private static void RebuildCoverageGrid(Map map) {
		EnsureDetectorDefsInitialized();
		GetOrCreateEmptyCoverageGrid(map).Rebuild(EnumerateActiveDetectors(map), true);
	}

	private static IEnumerable<(ThingWithComps Detector, float Radius)> EnumerateActiveDetectors(Map map) {
		EnsureDetectorDefsInitialized();
		foreach (var def in _detectorDefs) {
			foreach (var thing in map.listerThings.ThingsOfDef(def)) {
				if (thing is not ThingWithComps detector)
					continue;
				var comp = detector.GetComp<CompSendSignalOnMotion>();
				if (comp is not { Sent: false } || !detector.Spawned)
					continue;
				yield return (detector, comp.Props.radius);
			}
		}
	}

	internal sealed class DetectorCoverage(Map map) {
		public Map Map { get; } = map;

		public ByteGrid Grid { get; } = new(map);

		public HashSet<int> ActiveDetectorIds { get; } = [];

		public void Rebuild(IEnumerable<(ThingWithComps Detector, float Radius)> detectors, bool recalculatePaths) {
			Clear();
			Populate(detectors, recalculatePaths);
		}

		public void Populate(IEnumerable<(ThingWithComps Detector, float Radius)> detectors, bool recalculatePaths) {
			if (!recalculatePaths) {
				foreach ((var detector, float radius) in detectors)
					SetDetectorCoverage(detector, radius, true, false);
				return;
			}
			using var _ = Map.pathing.DisableIncrementalScope();
			foreach ((var detector, float radius) in detectors)
				SetDetectorCoverage(detector, radius, true, true);
		}

		public void SetDetectorCoverage(ThingWithComps detector, float detectionRadius, bool active, bool recalculatePaths) {
			int detectorId = detector.thingIDNumber;
			if (active) {
				if (!ActiveDetectorIds.Add(detectorId))
					return;
				ApplyCoverageDelta(detector.Position, detectionRadius, 1, recalculatePaths);
				return;
			}
			if (!ActiveDetectorIds.Remove(detectorId))
				return;
			ApplyCoverageDelta(detector.Position, detectionRadius, -1, recalculatePaths);
		}

		public void RefreshCoveredCells() {
			if (ActiveDetectorIds.Count == 0)
				return;
			using var _ = Map.pathing.DisableIncrementalScope();
			for (var i = 0; i < Grid.CellsCount; i++) {
				if (Grid[i] <= 0)
					continue;
				Map.pathing.RecalculatePerceivedPathCostAt(Map.cellIndices[i]);
			}
		}

		public void Clear() {
			Grid.Clear();
			ActiveDetectorIds.Clear();
		}

		private void ApplyCoverageDelta(IntVec3 center, float radius, int delta, bool recalculatePaths) {
			float effectiveRadius = radius + RADIUS_MARGIN;
			int r = Mathf.CeilToInt(effectiveRadius);
			float rSq = effectiveRadius * effectiveRadius;
			for (int dx = -r; dx <= r; dx++) {
				for (int dz = -r; dz <= r; dz++) {
					if (dx * dx + dz * dz > rSq)
						continue;
					var cell = new IntVec3(center.x + dx, 0, center.z + dz);
					if (!cell.InBounds(Map))
						continue;
					byte prevCount = Grid[cell];
					byte newCount;
					if (delta > 0) {
						if (prevCount == byte.MaxValue) {
							Helper.Logger.Warning(
								"Auto Avoid Mech Detectors coverage overflowed ByteGrid capacity; coverage may be approximate.",
								true
							);
							continue;
						}
						newCount = (byte)(prevCount + 1);
					}
					else {
						if (prevCount == 0)
							continue;
						newCount = (byte)(prevCount - 1);
					}
					Grid[cell] = newCount;
					if (recalculatePaths && (prevCount == 0) != (newCount == 0))
						Map.pathing.RecalculatePerceivedPathCostAt(cell);
				}
			}
		}
	}
}