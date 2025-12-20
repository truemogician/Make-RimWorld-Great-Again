using System.Linq;
using HarmonyLib;
using TrueMogician.RimWorld.Rimsonable.Patches;
using TrueMogician.RimWorld.Rimsonable.Static;
using VEF.Apparels;
using Verse;

// ReSharper disable InconsistentNaming

namespace TrueMogician.RimWorld.Rimsonable.VEF.Patches;

[HarmonyPatch]
public static class CompShieldBubblePatches {
	[HarmonyPatch(typeof(CompShieldBubble), nameof(CompShieldBubble.CompAllowVerbCast))]
	[HarmonyPriority(Priority.First)]
	[HarmonyPrefix]
	public static bool Prefix(CompShieldBubble __instance, Verb verb, ref bool __result) {
		if (!__instance.Props.dontAllowRangedAttack)
			return true;
		if (!CompShieldPatches.LaunchVerbs.Any(t => t.IsInstanceOfType(verb)))
			return true;
		if (verb.EquipmentSource?.def.thingCategories is not { } categories || !categories.Contains(Defs.Grenades))
			return true;
		__result = true; // Allow grenades
		return false;    // Skip the original method
	}
}