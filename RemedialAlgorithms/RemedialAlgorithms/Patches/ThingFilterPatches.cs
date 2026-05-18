using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.RemedialAlgorithms.Patches;

/// <summary>
///     Suppress per-descendant <see cref="ThingFilter.settingsChangedCallback" /> firings during
///     bulk <c>SetAllow</c> overloads; fire once at the end instead.
/// </summary>
[HarmonyPatch(typeof(ThingFilter))]
public static class ThingFilterPatches {
	private static readonly AccessTools.FieldRef<ThingFilter, Action?> _callbackRef =
		AccessTools.FieldRefAccess<ThingFilter, Action?>("settingsChangedCallback");

	[HarmonyPatch(
		nameof(ThingFilter.SetAllow),
		typeof(ThingCategoryDef),
		typeof(bool),
		typeof(IEnumerable<ThingDef>),
		typeof(IEnumerable<SpecialThingFilterDef>)
	)]
	[HarmonyPrefix]
	public static void SetAllow_Category_Prefix(ThingFilter __instance, out Action? __state) {
		__state = _callbackRef(__instance);
		_callbackRef(__instance) = null;
	}

	[HarmonyPatch(
		nameof(ThingFilter.SetAllow),
		typeof(ThingCategoryDef),
		typeof(bool),
		typeof(IEnumerable<ThingDef>),
		typeof(IEnumerable<SpecialThingFilterDef>)
	)]
	[HarmonyFinalizer]
	public static void SetAllow_Category_Finalizer(ThingFilter __instance, Action? __state) {
		_callbackRef(__instance) = __state;
		__state?.Invoke();
	}

	[HarmonyPatch(nameof(ThingFilter.SetAllow), typeof(StuffCategoryDef), typeof(bool))]
	[HarmonyPrefix]
	public static void SetAllow_StuffCategory_Prefix(ThingFilter __instance, out Action? __state) {
		__state = _callbackRef(__instance);
		_callbackRef(__instance) = null;
	}

	[HarmonyPatch(nameof(ThingFilter.SetAllow), typeof(StuffCategoryDef), typeof(bool))]
	[HarmonyFinalizer]
	public static void SetAllow_StuffCategory_Finalizer(ThingFilter __instance, Action? __state) {
		_callbackRef(__instance) = __state;
		__state?.Invoke();
	}
}