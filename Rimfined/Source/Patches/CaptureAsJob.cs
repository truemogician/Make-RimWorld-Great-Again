using System.Collections.Generic;
using HarmonyLib;
using TrueMogician.RimWorld.Rimfined.Components;
using TrueMogician.RimWorld.Utility;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.Rimfined.Patches;

internal static class CaptureAsJobPatches {
	private static readonly Texture2D CaptureIcon = ContentFinder<Texture2D>.Get("UI/Commands/Capture");

	[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
	[HarmonyPostfix]
	public static IEnumerable<Gizmo> Pawn_GetGizmos_Postfix(IEnumerable<Gizmo> __result, Pawn __instance) {
		foreach (var g in __result)
			yield return g;
		if (!PawnsToCapture.ValidForCapture(__instance))
			yield break;
		if (__instance.Map is not { } map || CachedMapComponent<PawnsToCapture>.Get(map) is not { } comp)
			yield break;
		yield return new Command_Toggle {
			defaultLabel = "Capture",
			defaultDesc = "Wardens will capture this downed pawn when a prisoner bed is available.",
			icon = CaptureIcon,
			isActive = () => comp[__instance],
			toggleAction = () => comp.Toggle(__instance)
		};
	}
}