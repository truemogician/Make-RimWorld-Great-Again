using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.AnimalHaulExtended;

public static class EventPatches {
	public static void UpdateExtraHaulTargetGrid(Map map, CellRect rect, HaulTarget target, bool add) {
		if (map.GetHaulTargetCellCollection() is not { } component)
			return;
		if (add)
			component.Add(rect, target);
		else
			component.Remove(rect, target);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateExtraHaulTargetGrid(Map map, Thing thing, HaulTarget target, bool add)
		=> UpdateExtraHaulTargetGrid(map, thing.OccupiedRect(), target, add);

	#region Construction Site Patches
	[HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.PlaceBlueprintForBuild))]
	[HarmonyPostfix]
	internal static void GenConstruct_PlaceBlueprintForBuild_Postfix(Blueprint_Build __result) {
		if (__result is not { Spawned: true, Map: { } map })
			return;
		UpdateExtraHaulTargetGrid(map, __result, HaulTarget.ConstructionSite, true);
	}
	#endregion

	#region Transporter Patches
	[HarmonyPatch(typeof(CompTransporter), nameof(CompTransporter.AddToTheToLoadList))]
	[HarmonyPostfix]
	internal static void CompTransporter_AddToTheToLoadList_Postfix(CompTransporter __instance)
		=> UpdateTransporterFlag(__instance);

	[HarmonyPatch(typeof(CompTransporter), nameof(CompTransporter.CancelLoad), typeof(Map))]
	[HarmonyPostfix]
	internal static void CompTransporter_CancelLoad_Map_Postfix(CompTransporter __instance)
		=> UpdateTransporterFlag(__instance);

	[HarmonyPatch(typeof(CompTransporter), nameof(CompTransporter.SubtractFromToLoadList))]
	[HarmonyPostfix]
	internal static void CompTransporter_SubtractFromToLoadList_Postfix(CompTransporter __instance)
		=> UpdateTransporterFlag(__instance);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void UpdateTransporterFlag(CompTransporter comp) {
		if (comp.parent is not { Spawned: true, Map: { } map } parent)
			return;
		UpdateExtraHaulTargetGrid(map, parent, HaulTarget.Transporter, comp.AnythingLeftToLoad);
	}
	#endregion
}