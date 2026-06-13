using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.FlippedBuildings.Core;
using TrueMogician.RimWorld.FlippedBuildings.Static;
using UnityEngine;
using Verse;
using Verse.Sound;
using Verse.Steam;

namespace TrueMogician.RimWorld.FlippedBuildings.Patches;

// Flip swaps the designator's entDef between canonical and twin in place, so ghost, validation, and
// placement all follow with no further patching; rotation is preserved. The button sits in the rotate panel.
[HarmonyPatch(typeof(Designator_Place))]
internal static class DesignatorPlacePatches {
	private const int _WINDOW_ID = 74100;

	private const float _BUTTON = 64f;

	private const float _GAP = 5f;

	private const float _PADDING = 10f;

	private static readonly AccessTools.FieldRef<Designator_Build, BuildableDef> EntDef =
		AccessTools.FieldRefAccess<Designator_Build, BuildableDef>("entDef");

	private static readonly AccessTools.FieldRef<Designator_Place, Rot4> PlacingRot =
		AccessTools.FieldRefAccess<Designator_Place, Rot4>("placingRot");

	public static bool CanFlip(ThingDef def) =>
		FlipRegistry.GetTwin(def) != null && Mod.Settings.IsFlipAllowed(FlipRegistry.Canonicalize(def));

	public static void Toggle(Designator_Build designator) {
		if (EntDef(designator) is ThingDef def && FlipRegistry.GetTwin(def) is { } twin) {
			EntDef(designator) = twin;
			SoundDefOf.DragSlider.PlayOneShotOnCamera();
		}
	}

	// Replaces the vanilla rotate-only panel with a combined rotate+flip panel for flippable defs.
	[HarmonyPatch(nameof(Designator_Place.DoExtraGuiControls))]
	[HarmonyPrefix]
	internal static bool DoExtraGuiControls_Prefix(Designator_Place __instance, float leftX, float bottomY) {
		if (__instance is not Designator_Build designator || !CanFlip(designator))
			return true;
		if (__instance.PlacingDef.PlaceWorkers != null) {
			foreach (var placeWorker in __instance.PlacingDef.PlaceWorkers)
				placeWorker.DrawOnGUIExtra(__instance.PlacingDef);
		}
		DrawControls(designator, leftX, bottomY);
		return false;
	}

	[HarmonyPatch(nameof(Designator_Place.SelectedProcessInput))]
	[HarmonyPostfix]
	internal static void SelectedProcessInput_Postfix(Designator_Place __instance) {
		if (__instance is Designator_Build designator && CanFlip(designator) && FlipKeyBindingDefOf.FlippedBuildings_Flip.KeyDownEvent) {
			Toggle(designator);
			Event.current.Use();
		}
	}

	[HarmonyPatch(nameof(Designator_Place.Selected))]
	[HarmonyPostfix]
	internal static void Selected_Postfix(Designator_Place __instance) {
		if (__instance is Designator_Build designator)
			ResetToCanonical(designator);
	}

	private static bool CanFlip(Designator_Build designator) => EntDef(designator) is ThingDef def && CanFlip(def);

	private static void ResetToCanonical(Designator_Build designator) {
		if (EntDef(designator) is ThingDef def && FlipRegistry.GetCanonical(def) is { } canonical)
			EntDef(designator) = canonical;
	}

	private static void DrawControls(Designator_Build designator, float leftX, float bottomY) {
		bool rotatable = designator.PlacingDef is ThingDef { rotatable: true };
		int count = rotatable ? 3 : 1;
		float width = count * _BUTTON + (count - 1) * _GAP + 2f * _PADDING;
		var winRect = new Rect(leftX, bottomY - 90f, width, 90f);
		Find.WindowStack.ImmediateWindow(
			_WINDOW_ID,
			winRect,
			WindowLayer.GameUI,
			() => {
				using (new TextBlock(GameFont.Medium, TextAnchor.MiddleCenter)) {
					var x = _PADDING;
					if (rotatable) {
						DrawRotateButton(new Rect(x, 15f, _BUTTON, _BUTTON), designator, RotationDirection.Counterclockwise);
						x += _BUTTON + _GAP;
						DrawRotateButton(new Rect(x, 15f, _BUTTON, _BUTTON), designator, RotationDirection.Clockwise);
						x += _BUTTON + _GAP;
					}
					DrawFlipButton(new Rect(x, 15f, _BUTTON, _BUTTON), designator);
				}
			}
		);
	}

	private static void DrawRotateButton(Rect rect, Designator_Build designator, RotationDirection dir) {
		var tex = dir == RotationDirection.Counterclockwise ? TexUI.RotLeftTex : TexUI.RotRightTex;
		var keyDef = dir == RotationDirection.Counterclockwise ? KeyBindingDefOf.Designator_RotateLeft : KeyBindingDefOf.Designator_RotateRight;
		if (Widgets.ButtonImage(rect, tex)) {
			SoundDefOf.DragSlider.PlayOneShotOnCamera();
			PlacingRot(designator).Rotate(dir);
			Event.current.Use();
		}
		if (!SteamDeck.IsSteamDeck)
			Widgets.Label(rect, keyDef.MainKeyLabel);
	}

	private static void DrawFlipButton(Rect rect, Designator_Build designator) {
		// TODO: replace TexUI.Placeholder with a dedicated flip icon once the texture is authored.
		if (Widgets.ButtonImage(rect, TexUI.Placeholder)) {
			Toggle(designator);
			Event.current.Use();
		}
		if (!SteamDeck.IsSteamDeck)
			Widgets.Label(rect, FlipKeyBindingDefOf.FlippedBuildings_Flip.MainKeyLabel);
		TooltipHandler.TipRegion(
			rect,
			() => "FlippedBuildings.FlipTip".Translate(FlipKeyBindingDefOf.FlippedBuildings_Flip.MainKeyLabel).Resolve(),
			_WINDOW_ID
		);
	}
}