using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.FlippedBuildings.Core;

namespace TrueMogician.RimWorld.FlippedBuildings.Patches;

// Prefix so twins exist before vanilla's implied-def pass generates their blueprint/frame defs.
[HarmonyPatch(typeof(DefGenerator))]
internal static class DefGeneratorPatches {
	[HarmonyPatch(nameof(DefGenerator.GenerateImpliedDefs_PreResolve))]
	[HarmonyPrefix]
	private static void GenerateImpliedDefs_PreResolve_Prefix() => FlipDefGenerator.GenerateAll();
}