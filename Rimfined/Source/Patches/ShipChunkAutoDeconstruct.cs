using HarmonyLib;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.Rimfined.Patches;

internal static class ShipChunkAutoDeconstructPatches {
	[HarmonyPatch(typeof(Building), nameof(Building.SpawnSetup))]
	[HarmonyPostfix]
	internal static void Building_SpawnSetup_Postfix(Building __instance, bool respawningAfterLoad) {
		if (respawningAfterLoad)
			return;
		var def = __instance.def;
		if (def != ThingDefOf.ShipChunk && def != ThingDefOf.ShipChunk_Mech)
			return;
		var dm = __instance.Map.designationManager;
		if (dm.DesignationOn(__instance, DesignationDefOf.Deconstruct) != null)
			return;
		dm.AddDesignation(new Designation(__instance, DesignationDefOf.Deconstruct));
	}
}