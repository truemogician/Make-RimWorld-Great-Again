using System.Collections.Generic;
using HarmonyLib;
using TacticalGroups;
using UnityEngine;
using Verse;
using UsefulMarksDrawPawnLabelPatch = UsefulMarks.NamePlatePatches.GenMapUI_DrawPawnLabel_Patch;

namespace TrueMogician.RimWorld.UsefulMarksInColonyGroups;

[HarmonyPatch(typeof(TacticalGroups_ColonistBarColonistDrawer))]
public static class Patches {
	private static readonly Dictionary<string, string> TruncatedLabelsCache = [];

	[HarmonyPatch(nameof(TacticalGroups_ColonistBarColonistDrawer.DrawColonist))]
	[HarmonyPostfix]
	internal static void TacticalGroups_ColonistBarColonistDrawer_DrawColonist_Postfix(Rect rect, Pawn? colonist) {
		if (colonist is null or { Dead: true })
			return;
		var wasDrawingAtColonistBar = UsefulMarksDrawPawnLabelPatch.DrawingAtColonistBar;
		try {
			UsefulMarksDrawPawnLabelPatch.DrawingAtColonistBar = true;
			UsefulMarksDrawPawnLabelPatch.Postfix(colonist, GetPawnLabelBackgroundRect(rect, colonist));
		}
		finally {
			UsefulMarksDrawPawnLabelPatch.DrawingAtColonistBar = wasDrawingAtColonistBar;
		}
	}

	private static Rect GetPawnLabelBackgroundRect(Rect rect, Pawn colonist) {
		float scale = TacticUtils.TacticalColonistBar.Scale;
		float truncateToWidth = rect.width + TacticUtils.TacticalColonistBar.SpaceBetweenColonistsHorizontal - 2f;
		float labelWidth = GetPawnLabelWidth(colonist, truncateToWidth);
		var pos = new Vector2(rect.center.x, rect.yMax - 4f * scale);
		return new Rect(pos.x - labelWidth / 2f - 4f, pos.y, labelWidth + 8f, 12f);
	}

	private static float GetPawnLabelWidth(Pawn colonist, float truncateToWidth) {
		var font = Text.Font;
		Text.Font = GameFont.Tiny;
		try {
			float labelWidth = colonist.LabelShortCap.Truncate(truncateToWidth, TruncatedLabelsCache).GetWidthCached();
			if (Mathf.Abs(Mathf.Round(Prefs.UIScale) - Prefs.UIScale) > float.Epsilon)
				labelWidth += 0.5f;
			return Mathf.Max(labelWidth, 20f);
		}
		finally {
			Text.Font = font;
		}
	}
}