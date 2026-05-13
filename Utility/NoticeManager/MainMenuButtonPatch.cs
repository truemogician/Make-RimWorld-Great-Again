using HarmonyLib;
using TrueMogician.RimWorld.Utility.Attributes;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Utility.NoticeManager;

[StaticConstructorOnStartup]
internal static class MainMenuButtonPatch {
	static MainMenuButtonPatch() {
		var harmony = new Harmony(typeof(NoticeManager).Assembly.GetName().Name);
		harmony.PatchFromType(typeof(MainMenuButtonPatch));
	}

	[HarmonyPatch(typeof(ListableOption), nameof(ListableOption.DrawOption))]
	[HarmonyPrefix]
	private static bool DrawOption_Prefix(ListableOption __instance, Vector2 pos, float width, ref float __result) =>
		!NoticeManager.TryDrawModsOptionWithNotice(__instance, pos, width, ref __result);
}