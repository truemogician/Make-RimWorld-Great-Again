using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

// ReSharper disable InconsistentNaming

namespace TrueMogician.RimWorld.Rimsonable.Patches;

[HarmonyPatch(typeof(CompShield))]
public static class CompShieldPatches {
	private static readonly ThingCategoryDef Grenades = DefDatabase<ThingCategoryDef>.GetNamed("Grenades");

	private static readonly HashSet<Type> LaunchVerbs = [typeof(Verb_LaunchProjectile)];

	public static void AddLaunchVerb(Type verbType) {
		LaunchVerbs.Add(verbType);
	}

	[HarmonyPatch(nameof(CompShield.CompAllowVerbCast))]
	[HarmonyPriority(Priority.First)]
	[HarmonyPrefix]
	public static bool CompAllowVerbCast_Prefix(CompShield __instance, Verb verb, ref bool __result) {
		if (!__instance.Props.blocksRangedWeapons)
			return true;
		if (!LaunchVerbs.Any(t => t.IsInstanceOfType(verb)))
			return true;
		if (verb.EquipmentSource?.def.thingCategories is not { } categories || !categories.Contains(Grenades))
			return true;
		__result = true; // Allow grenades
		return false;    // Skip the original method
	}
}