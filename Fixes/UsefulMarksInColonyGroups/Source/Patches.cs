using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TacticalGroups;
using UnityEngine;
using Verse;
using UF = UsefulMarks;

namespace TrueMogician.RimWorld.UsefulMarksInColonyGroups;

[HarmonyPatch(typeof(TacticalGroups_ColonistBarColonistDrawer))]
public static class Patches {
	private const float _BOTTOM_MARK_GAP_BELOW_WEAPON = 2f;

	private static readonly Dictionary<string, string> TruncatedLabelsCache = [];
	private static readonly Type? TacticalGroupsSettingsType = AccessTools.TypeByName("TacticalGroups.TacticalGroupsSettings");
	private static readonly Func<bool>? GetDisplayWeapons = CreateStaticFieldGetter<bool>("DisplayWeapons");
	private static readonly Func<int>? GetWeaponPlacementOffset = CreateStaticFieldGetter<int>("WeaponPlacementOffset");
	private static readonly Func<float>? GetWeaponShowScale = CreateStaticFieldGetter<float>("WeaponShowScale");
	private static readonly Func<WeaponShowMode>? GetWeaponShowMode = CreateStaticFieldGetter<WeaponShowMode>("WeaponShowMode");

	[HarmonyPatch(nameof(TacticalGroups_ColonistBarColonistDrawer.DrawColonist))]
	[HarmonyPostfix]
	internal static void TacticalGroups_ColonistBarColonistDrawer_DrawColonist_Postfix(Rect rect, Pawn? colonist) {
		if (colonist is null or { Dead: true })
			return;
		var prevState = UF.NamePlatePatches.GenMapUI_DrawPawnLabel_Patch.DrawingAtColonistBar;
		var prevOffset = UF.ColonistBar_DrawWeapon_Patch.WeaponYOffset;
		var prevMode = Prefs.ShowWeaponsUnderPortraitMode;
		try {
			UpdateUsefulMarksWeaponState(rect, colonist);
			UF.NamePlatePatches.GenMapUI_DrawPawnLabel_Patch.DrawingAtColonistBar = true;
			UF.NamePlatePatches.GenMapUI_DrawPawnLabel_Patch.Postfix(colonist, GetPawnLabelBackgroundRect(rect, colonist));
		}
		finally {
			Prefs.ShowWeaponsUnderPortraitMode = prevMode;
			UF.ColonistBar_DrawWeapon_Patch.WeaponYOffset = prevOffset;
			UF.NamePlatePatches.GenMapUI_DrawPawnLabel_Patch.DrawingAtColonistBar = prevState;
		}
	}

	private static void UpdateUsefulMarksWeaponState(Rect rect, Pawn colonist) {
		if (!UF.Settings.BottomMarksUnderWeaponOnPortrait)
			return;
		if (!TryGetTacticalGroupsWeaponRect(rect, colonist, out var weaponRect)) {
			Prefs.ShowWeaponsUnderPortraitMode = ShowWeaponsUnderPortraitMode.Never;
			return;
		}
		Prefs.ShowWeaponsUnderPortraitMode = ShowWeaponsUnderPortraitMode.Always;
		UF.ColonistBar_DrawWeapon_Patch.WeaponYOffset = weaponRect.yMax + (8f + _BOTTOM_MARK_GAP_BELOW_WEAPON) * (1f / Prefs.UIScale);
	}

	private static Rect GetPawnLabelBackgroundRect(Rect rect, Pawn colonist) {
		float scale = TacticUtils.TacticalColonistBar.Scale;
		float truncateToWidth = rect.width + TacticUtils.TacticalColonistBar.SpaceBetweenColonistsHorizontal - 2f;
		float width = GetPawnLabelWidth(colonist, truncateToWidth);
		var pos = new Vector2(rect.center.x, rect.yMax - 4f * scale);
		return new Rect(pos.x - width / 2f - 4f, pos.y, width + 8f, 12f);
	}

	private static float GetPawnLabelWidth(Pawn colonist, float truncateToWidth) {
		float width;
		using (new TextBlock(GameFont.Tiny)) {
			width = colonist.LabelShortCap.Truncate(truncateToWidth, TruncatedLabelsCache).GetWidthCached();
			if (Mathf.Abs(Mathf.Round(Prefs.UIScale) - Prefs.UIScale) > float.Epsilon)
				width += 0.5f;
		}
		return Mathf.Max(width, 20f);
	}

	private static bool TryGetTacticalGroupsWeaponRect(Rect rect, Pawn colonist, out Rect weaponRect) {
		weaponRect = default;
		if (GetDisplayWeapons?.Invoke() != true || colonist.Dead || colonist.Downed)
			return false;
		if (colonist.equipment?.Primary is not { def.IsWeapon: true })
			return false;
		if (GetWeaponShowMode?.Invoke() == WeaponShowMode.Drafted && !colonist.Drafted)
			return false;
		if (colonist.TryGetGroups(out HashSet<ColonistGroup> groups) && groups.Any(x => x.hideWeaponOverlay))
			return false;
		float size = rect.width * (GetWeaponShowScale?.Invoke() ?? 1f);
		float xPos = rect.x - ((size - rect.width) / 2f);
		weaponRect = new Rect(xPos, rect.yMax + (GetWeaponPlacementOffset?.Invoke() ?? -10), size, size);
		return true;
	}

	private static Func<T>? CreateStaticFieldGetter<T>(string fieldName) {
		if (TacticalGroupsSettingsType is null)
			return null;
		var field = AccessTools.Field(TacticalGroupsSettingsType, fieldName);
		if (field is null)
			return null;
		return () => (T)field.GetValue(null)!;
	}
}