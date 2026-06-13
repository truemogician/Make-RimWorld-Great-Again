using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.FlippedBuildings.Core;
using Verse;

namespace TrueMogician.RimWorld.FlippedBuildings.Patches;

// Twins have no architect designator, so build-copy/rebuild lookups fall back to the canonical's.
// The copy places the canonical building; the player re-flips at placement.
[HarmonyPatch(typeof(BuildCopyCommandUtility))]
internal static class BuildCopyCommandUtilityPatches {
	[HarmonyPatch(nameof(BuildCopyCommandUtility.FindAllowedDesignator))]
	[HarmonyPostfix]
	internal static void FindAllowedDesignator_Postfix(BuildableDef buildable, bool mustBeVisible, ref Designator_Build? __result) {
		if (__result != null || buildable is not ThingDef def)
			return;
		if (FlipRegistry.GetCanonical(def) is { } canonical)
			__result = BuildCopyCommandUtility.FindAllowedDesignator(canonical, mustBeVisible);
	}
}