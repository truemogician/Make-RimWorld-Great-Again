using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.FlippedBuildings.Core;
using Verse;

namespace TrueMogician.RimWorld.FlippedBuildings.Patches;

// Single-def comparison sites where a twin would read as a different building. List-driven systems
// (recipes, facility links) are bridged at generation time; only these direct def-equality checks remain.
[HarmonyPatch]
internal static class IdentityBridgingPatches {
	[HarmonyPatch(typeof(ListerBuildings), nameof(ListerBuildings.ColonistsHaveBuilding), typeof(ThingDef))]
	[HarmonyPostfix]
	internal static void ListerBuildings_ColonistsHaveBuilding_Postfix(ThingDef def, ListerBuildings __instance, ref bool __result) {
		if (__result || FlipRegistry.GetTwin(def) is not { } twin)
			return;
		foreach (var building in __instance.allBuildingsColonist) {
			if (building.def == twin) {
				__result = true;
				return;
			}
		}
	}

	[HarmonyPatch(typeof(ResearchProjectDef), nameof(ResearchProjectDef.CanBeResearchedAt))]
	[HarmonyPostfix]
	internal static void ResearchProjectDef_CanBeResearchedAt_Postfix(
		ResearchProjectDef __instance,
		Building_ResearchBench bench,
		bool ignoreResearchBenchPowerStatus,
		ref bool __result
	) {
		if (__result || FlipRegistry.GetCanonical(bench.def) is not { } canonical)
			return;
		if (__instance.requiredResearchBuilding != null && canonical != __instance.requiredResearchBuilding)
			return;
		if (!ignoreResearchBenchPowerStatus && bench.GetComp<CompPowerTrader>() is { PowerOn: false })
			return;
		if (!__instance.requiredResearchFacilities.NullOrEmpty()) {
			var affected = bench.TryGetComp<CompAffectedByFacilities>();
			if (affected == null)
				return;
			var linked = affected.LinkedFacilitiesListForReading;
			foreach (var required in __instance.requiredResearchFacilities) {
				var satisfied = false;
				foreach (var facility in linked) {
					if ((facility.def == required || FlipRegistry.GetCanonical(facility.def) == required)
						&& affected.IsFacilityActive(facility)) {
						satisfied = true;
						break;
					}
				}
				if (!satisfied)
					return;
			}
		}
		__result = true;
	}

	// Safety net beyond the gen-time virtualDefs link: a twin is allowed when its canonical source is,
	// for filters written directly to allowedDefs in code or older saves.
	[HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.Allows), typeof(ThingDef))]
	[HarmonyPostfix]
	internal static void ThingFilter_Allows_Postfix(ThingDef? def, ThingFilter __instance, ref bool __result) {
		if (__result || def == null || !FlipRegistry.IsFlipped(def))
			return;
		if (FlipRegistry.GetCanonical(def) is { } canonical && __instance.Allows(canonical))
			__result = true;
	}

	// Twins are hidden from the architect via canGenerateDefaultDesignator=false, but vanilla Ideology reads that
	// same flag as "freely buildable by any ideoligion". Mirror the canonical so colonists deliver and construct.
	[HarmonyPatch(typeof(Ideo), nameof(Ideo.MembersCanBuild), typeof(Thing))]
	[HarmonyPostfix]
	internal static void Ideo_MembersCanBuild_Postfix(Thing thing, Ideo __instance, ref bool __result) {
		if (__result)
			return;
		var built = thing.def.entityDefToBuild as ThingDef ?? thing.def;
		if (FlipRegistry.GetCanonical(built) is not { } canonical)
			return;
		__result = canonical.canGenerateDefaultDesignator
			|| __instance.cachedPossibleBuildables.Contains(canonical)
			|| __instance.HasPreceptForBuilding(canonical);
	}
}