using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.FlippedBuildings.Core;
using Verse;

namespace TrueMogician.RimWorld.FlippedBuildings.Patches;

// The fueling port is computed in code with no def field to mirror. This overload carries no def, so resolve
// the owner from the launcher at center (in-world) or the active designator (ghost). The (Building) overload delegates here.
[HarmonyPatch(typeof(FuelingPortUtility))]
internal static class FuelingPortUtilityPatches {
	[HarmonyPatch(nameof(FuelingPortUtility.GetFuelingPortCell), typeof(IntVec3), typeof(Rot4))]
	[HarmonyPostfix]
	private static void GetFuelingPortCell_Postfix(IntVec3 center, ref IntVec3 __result) {
		if (ResolveFuelingPortDef(center) is { } def && FlipRegistry.IsFlipped(def))
			__result = center + MirrorTransform.MirrorCellOffset(__result - center, def.size);
	}

	private static ThingDef? ResolveFuelingPortDef(IntVec3 center) {
		var map = Find.CurrentMap;
		if (map != null && center.InBounds(map) && center.GetFirstBuilding(map) is { } building && building.def.building is { hasFuelingPort: true })
			return building.def;
		return Find.DesignatorManager?.SelectedDesignator is Designator_Build { PlacingDef: ThingDef { building.hasFuelingPort: true } placing }
			? placing
			: null;
	}
}