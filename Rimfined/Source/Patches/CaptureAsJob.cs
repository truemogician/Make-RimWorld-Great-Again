using System.Collections.Generic;
using HarmonyLib;
using TrueMogician.RimWorld.Rimfined.Components;
using TrueMogician.RimWorld.Utility;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Rimfined.Patches;

[StaticConstructorOnStartup]
internal static class CaptureAsJobPatches {
	internal static readonly Texture2D CaptureIcon = ContentFinder<Texture2D>.Get("UI/Commands/Capture");

	internal const string TRANSLATION_KEY_PREFIX = "Rimfined.Commands.Capture";

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
			defaultLabel = $"{TRANSLATION_KEY_PREFIX}.label".Translate(),
			defaultDesc = $"{TRANSLATION_KEY_PREFIX}.description".Translate(),
			icon = CaptureIcon,
			isActive = () => comp[__instance],
			toggleAction = () => comp.Toggle(__instance)
		};
	}
}