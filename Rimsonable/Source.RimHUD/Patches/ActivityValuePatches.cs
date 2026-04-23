using HarmonyLib;
using RimHUD.Interface.Hud.Models;
using RimHUD.Interface.Hud.Models.Values;
using TrueMogician.RimWorld.Rimsonable.Patches;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable.RimHUD.Patches;

[HarmonyPatch(typeof(ActivityValue))]
public static class ActivityValuePatches {
	[HarmonyPatch("GetValue")]
	[HarmonyPostfix]
	internal static void GetValue_Postfix(ref string? __result) {
		if (__result.NullOrEmpty() || !WorkMemory.TryGetDisplay(Active.Pawn, "Rimsonable.WorkMemory.InspectPostfix", out var postfix))
			return;
		__result = $"{__result}<b>{postfix}</b>";
	}

	[HarmonyPatch("GetTooltip")]
	[HarmonyPostfix]
	internal static void GetTooltip_Postfix(ref string? __result) {
		if (!WorkMemory.TryGetDisplay(Active.Pawn, "Rimsonable.WorkMemory.InspectLine", out string line))
			return;
		__result = __result.NullOrEmpty() ? line : $"{__result}\n{line}";
	}
}
