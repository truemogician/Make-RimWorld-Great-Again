using HarmonyLib;
using RimHUD.Interface.Hud.Models;
using RimHUD.Interface.Hud.Models.Values;
using Verse;

namespace TrueMogician.RimWorld.WorkMemory.RimHUD.Patches;

using WorkMemoryPatches = WorkMemory.Patches.WorkMemory;

[HarmonyPatch(typeof(ActivityValue))]
public static class ActivityValuePatches {
	[HarmonyPatch("GetValue")]
	[HarmonyPostfix]
	internal static void GetValue_Postfix(ref string? __result) {
		if (__result.NullOrEmpty() || !WorkMemoryPatches.TryGetDisplay(Active.Pawn, "WorkMemory.InspectPostfix", out var postfix))
			return;
		__result = $"{__result}<b>{postfix}</b>";
	}

	[HarmonyPatch("GetTooltip")]
	[HarmonyPostfix]
	internal static void GetTooltip_Postfix(ref string? __result) {
		if (!WorkMemoryPatches.TryGetDisplay(Active.Pawn, "WorkMemory.InspectLine", out string line))
			return;
		__result = __result.NullOrEmpty() ? line : $"{__result}\n{line}";
	}
}
