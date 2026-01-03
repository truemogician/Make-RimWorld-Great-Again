using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Rimsonable.Static;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable.Patches;

[HarmonyPatch(typeof(CompShield))]
public static class AllowGrenadesThroughShields {
	private static readonly HashSet<Type> _launchVerbs = [typeof(Verb_LaunchProjectile)];

	public static IReadOnlyCollection<Type> LaunchVerbs => _launchVerbs;

	public static void AddLaunchVerb(Type verbType) {
		_launchVerbs.Add(verbType);
	}

	[HarmonyPatch(nameof(CompShield.CompAllowVerbCast))]
	[HarmonyPriority(Priority.First)]
	[HarmonyPrefix]
	public static bool CompAllowVerbCast_Prefix(CompShield __instance, Verb verb, ref bool __result) {
		if (!__instance.Props.blocksRangedWeapons)
			return true;
		if (!_launchVerbs.Any(t => t.IsInstanceOfType(verb)))
			return true;
		if (verb.EquipmentSource?.def.thingCategories is not { } categories || !categories.Contains(Defs.Grenades))
			return true;
		__result = true; // Allow grenades
		return false;    // Skip the original method
	}
}