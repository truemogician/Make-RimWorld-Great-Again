using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Rimsonable.Components;
using TrueMogician.RimWorld.Utility.Attributes;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable.Patches;

public static class EmergencyJobOverride {
	private const float _OVERRIDE_PRIORITY = 10f;

	private static List<WorkGiverDef>? _emergencyWorkGivers;

	internal static List<WorkGiverDef> EmergencyWorkGivers =>
		_emergencyWorkGivers ??= DefDatabase<WorkGiverDef>.AllDefsListForReading.Where(d => d.emergency).ToList();

	[HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.GetPriority))]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPostfix]
	internal static void JobGiver_Work_GetPriority_Postfix(JobGiver_Work __instance, Pawn pawn, ref float __result) {
		if (!__instance.emergency || __result >= _OVERRIDE_PRIORITY || !ShouldOverride(pawn))
			return;
		__result = _OVERRIDE_PRIORITY;
	}

	[HarmonyPatch(typeof(ThinkNode_ConditionalMustKeepLyingDown), "Satisfied")]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPostfix]
	internal static void MustKeepLyingDown_Satisfied_Postfix(Pawn pawn, ref bool __result) {
		if (!__result || !ShouldOverride(pawn))
			return;
		__result = false;
	}

	[PatchHook(PatchHookTiming.AfterPatch)]
	internal static void AfterPatch() {
		_ = EmergencyWorkGivers;
		if (Find.Maps is null)
			return;
		foreach (var map in Find.Maps)
			map.GetComponent<EmergencyJobDispatchTracker>()?.Refresh();
	}

	[PatchHook(PatchHookTiming.AfterUnpatch)]
	internal static void AfterUnpatch() {
		if (Find.Maps is null)
			return;
		foreach (var map in Find.Maps)
			map.GetComponent<EmergencyJobDispatchTracker>()?.Clear();
	}

	internal static bool ShouldOverride(Pawn? pawn) {
		if (pawn?.Map is not { } map)
			return false;
		if (map.GetComponent<EmergencyJobDispatchTracker>() is { } tracker && !tracker.IsDispatched(pawn))
			return false;
		return EmergencyJobDispatchTracker.IsInterruptible(pawn);
	}
}