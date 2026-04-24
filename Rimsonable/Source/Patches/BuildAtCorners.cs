using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.Rimsonable.Patches;

public static class BuildAtCorners {
	[HarmonyPatch(typeof(TouchPathEndModeUtility), nameof(TouchPathEndModeUtility.IsCornerTouchAllowed_NewTemp))]
	[HarmonyPrefix]
	internal static bool IsCornerTouchAllowed_NewTemp_Prefix(Thing? target, ref bool __result) {
		if (target is not (Blueprint or Frame))
			return true;
		__result = true;
		return false;
	}
}
