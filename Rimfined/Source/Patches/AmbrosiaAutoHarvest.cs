using HarmonyLib;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.Rimfined.Patches;

internal static class AmbrosiaAutoHarvestPatches {
	[HarmonyPatch(typeof(Plant), nameof(Plant.TickLong))]
	[HarmonyPostfix]
	internal static void Plant_TickLong_Postfix(Plant __instance) {
		if (__instance.def != ThingDefOf.Plant_Ambrosia)
			return;
		if (!__instance.HarvestableNow)
			return;
		var dm = __instance.Map.designationManager;
		if (dm.DesignationOn(__instance, DesignationDefOf.HarvestPlant) != null)
			return;
		dm.AddDesignation(new Designation(__instance, DesignationDefOf.HarvestPlant));
	}
}
