using System;
using HarmonyLib;
using RimWorld;
using Verse;

// ReSharper disable InconsistentNaming

namespace TrueMogician.RimWorld.Rimsonable.Patches;

[HarmonyPatch(typeof(CompShield))]
public static class CompShieldPatches {
	private static readonly ThingCategoryDef Grenades = DefDatabase<ThingCategoryDef>.GetNamed("Grenades");

	private static readonly Type? CELaunchVerbType = Type.GetType("CombatExtended.Verb_LaunchProjectileCE, CombatExtended", false);

	[HarmonyPatch(nameof(CompShield.CompAllowVerbCast))]
	[HarmonyPriority(Priority.First)]
	[HarmonyPrefix]
	public static bool CompAllowVerbCast_Prefix(CompShield __instance, Verb verb, ref bool __result) {
		if (!__instance.Props.blocksRangedWeapons)
			return true;
		if (verb is not Verb_LaunchProjectile && (CELaunchVerbType is null || !CELaunchVerbType.IsInstanceOfType(verb)))
			return true;
		if (verb.EquipmentSource?.def.thingCategories is not { } categories || !categories.Contains(Grenades))
			return true;
		__result = true; // Allow grenades
		return false;    // Skip the original method
	}
}