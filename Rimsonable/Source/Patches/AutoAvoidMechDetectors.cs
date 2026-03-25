using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.Rimsonable.Patches;

public static class AutoAvoidMechDetectors {
	public const int AVOID_COST = 800;

	public const float RADIUS_MARGIN = 2f;

	private static readonly List<ThingDef> _detectorDefs = [];

	static AutoAvoidMechDetectors() {
		LongEventHandler.ExecuteWhenFinished(() => {
			foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading) {
				if (def.HasComp<CompSendSignalOnMotion>())
					_detectorDefs.Add(def);
			}
		});
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
		var map = __instance.map;
		foreach (var def in _detectorDefs) {
			foreach (var thing in map.listerThings.ThingsOfDef(def)) {
				var comp = thing.TryGetComp<CompSendSignalOnMotion>();
				if (comp is not { Sent: false })
					continue;
				float effectiveRadius = comp.Props.radius + RADIUS_MARGIN;
				int dx = c.x - thing.Position.x;
				int dz = c.z - thing.Position.z;
				if (dx * dx + dz * dz <= effectiveRadius * effectiveRadius) {
					__result += AVOID_COST;
					return;
				}
			}
		}
	}

	[HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.SpawnSetup))]
	[HarmonyPostfix]
	private static void ThingWithComps_SpawnSetup_Postfix(ThingWithComps __instance) {
		var comp = __instance.GetComp<CompSendSignalOnMotion>();
		if (comp is not { Sent: false })
			return;
		RecalcPathsInRadius(__instance.Map, __instance.Position, comp.Props.radius);
	}

	[HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.DeSpawn))]
	[HarmonyPrefix]
	private static void ThingWithComps_DeSpawn_Prefix(ThingWithComps __instance) {
		var comp = __instance.GetComp<CompSendSignalOnMotion>();
		if (comp == null || !__instance.Spawned)
			return;
		RecalcPathsInRadius(__instance.Map, __instance.Position, comp.Props.radius);
	}

	// #region Trigger / Expire
	[HarmonyPatch(typeof(CompSendSignalOnMotion), "Trigger")]
	[HarmonyPostfix]
	private static void CompSendSignalOnMotion_Trigger_Postfix(CompSendSignalOnMotion __instance) {
		if (__instance.parent is { Spawned: true })
			RecalcPathsInRadius(__instance.parent.Map, __instance.parent.Position, __instance.Props.radius);
	}

	[HarmonyPatch(typeof(CompSendSignalOnMotion), nameof(CompSendSignalOnMotion.Notify_SignalReceived))]
	[HarmonyPostfix]
	private static void CompSendSignalOnMotion_Notify_SignalReceived_Postfix(CompSendSignalOnMotion __instance) {
		if (__instance is { Sent: true, parent.Spawned: true })
			RecalcPathsInRadius(__instance.parent.Map, __instance.parent.Position, __instance.Props.radius);
	}

	[HarmonyPatch(typeof(CompSendSignalOnMotion), nameof(CompSendSignalOnMotion.Expire))]
	[HarmonyPostfix]
	private static void CompSendSignalOnMotion_Expire_Postfix(CompSendSignalOnMotion __instance) {
		if (__instance.parent is { Spawned: true })
			RecalcPathsInRadius(__instance.parent.Map, __instance.parent.Position, __instance.Props.radius);
	}
	// #endregion

	private static void RecalcPathsInRadius(Map map, IntVec3 center, float detectionRadius) {
		int r = Mathf.CeilToInt(detectionRadius + RADIUS_MARGIN);
		int rSq = r * r;
		for (int dx = -r; dx <= r; dx++) {
			for (int dz = -r; dz <= r; dz++) {
				if (dx * dx + dz * dz <= rSq) {
					var cell = new IntVec3(center.x + dx, 0, center.z + dz);
					if (cell.InBounds(map))
						map.pathing.RecalculatePerceivedPathCostAt(cell);
				}
			}
		}
	}
}