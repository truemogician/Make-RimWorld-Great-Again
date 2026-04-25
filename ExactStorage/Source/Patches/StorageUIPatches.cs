using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage.Patches;

internal static class StorageUIPatches {
	private static readonly Func<ITab_Storage, IStoreSettingsParent> _selStoreSettingsParent =
		(Func<ITab_Storage, IStoreSettingsParent>)Delegate.CreateDelegate(
			typeof(Func<ITab_Storage, IStoreSettingsParent>),
			AccessTools.PropertyGetter(typeof(ITab_Storage), "SelStoreSettingsParent")
		);

	private static readonly FieldInfo _sizeField = AccessTools.Field(typeof(InspectTabBase), "size");

	private static readonly FieldInfo _winSizeField = AccessTools.Field(typeof(ITab_Storage), "WinSize");

	[HarmonyPatch(typeof(InspectTabBase), nameof(InspectTabBase.DoTabGUI))]
	[HarmonyPrefix]
	[HarmonyPriority(Priority.First)]
	internal static void InspectTabBase_DoTabGUI_Prefix(InspectTabBase __instance) {
		if (__instance is not ITab_Storage tab)
			return;
		var settings = _selStoreSettingsParent(tab)?.GetStoreSettings();
		var width = settings is null ? UI.BASE_WIDTH : UI.WindowWidth(settings);
		_winSizeField.SetValue(null, new Vector2(width, UI.BASE_HEIGHT + UI.FooterHeight(settings)));
		var size = (Vector2)_sizeField.GetValue(__instance);
		var winSize = (Vector2)_winSizeField.GetValue(null);
		size.x = winSize.x;
		size.y = winSize.y;
		_sizeField.SetValue(__instance, size);
	}

	[HarmonyPatch(typeof(ITab_Storage), "FillTab")]
	[HarmonyPrefix]
	[HarmonyPriority(Priority.First)]
	internal static void ITabStorage_FillTab_Prefix(ITab_Storage __instance) {
		UI.CurrentSettings = _selStoreSettingsParent(__instance)?.GetStoreSettings();
		var width = UI.CurrentSettings is null ? UI.BASE_WIDTH : UI.WindowWidth(UI.CurrentSettings);
		_winSizeField.SetValue(null, new Vector2(width, UI.BASE_HEIGHT));
	}

	[HarmonyPatch(typeof(ITab_Storage), "FillTab")]
	[HarmonyPostfix]
	[HarmonyPriority(Priority.First)]
	internal static void ITabStorage_FillTab_EarlyPostfix() {
		if (UI.CurrentSettings is { } settings) {
			UI.DrawFooter(settings);
			_winSizeField.SetValue(null, new Vector2(UI.WindowWidth(settings), UI.BASE_HEIGHT + UI.FooterHeight(settings)));
		}
	}

	[HarmonyPatch(typeof(ITab_Storage), "FillTab")]
	[HarmonyPostfix]
	[HarmonyPriority(Priority.Last)]
	internal static void ITabStorage_FillTab_LatePostfix() {
		if (UI.CurrentSettings is { } settings)
			_winSizeField.SetValue(null, new Vector2(UI.WindowWidth(settings), UI.BASE_HEIGHT));
		UI.CurrentSettings = null;
		UI.CurrentRow = null;
	}

	[HarmonyPatch(typeof(Widgets), nameof(Widgets.FloatRange))]
	[HarmonyPrefix]
	internal static void Widgets_FloatRange_Prefix(ref Rect rect) {
		if (UI.CurrentSettings is not null)
			rect.width -= 18f;
	}

	[HarmonyPatch(typeof(Widgets), nameof(Widgets.QualityRange))]
	[HarmonyPrefix]
	internal static void Widgets_QualityRange_Prefix(ref Rect rect) {
		if (UI.CurrentSettings is not null)
			rect.width -= 18f;
	}

	[HarmonyPatch(typeof(Listing_Tree), "get_LabelWidth")]
	[HarmonyPostfix]
	internal static void ListingTree_LabelWidth_Postfix(Listing_Tree __instance, ref float __result) {
		if (UI.Active && __instance is Listing_TreeThingFilter)
			__result -= UI.CONTROLS_WIDTH;
	}

	[HarmonyPatch(typeof(Listing_TreeThingFilter), "DoCategory")]
	[HarmonyPrefix]
	internal static void ListingTreeThingFilter_DoCategory_Prefix(TreeNode_ThingCategory node) => UI.CurrentRow = node;

	[HarmonyPatch(typeof(Listing_TreeThingFilter), "DoThingDef")]
	[HarmonyPrefix]
	internal static void ListingTreeThingFilter_DoThingDef_Prefix(ThingDef tDef) => UI.CurrentRow = tDef;

	[HarmonyPatch(typeof(Listing_TreeThingFilter), "DoSpecialFilter")]
	[HarmonyPrefix]
	internal static void ListingTreeThingFilter_DoSpecialFilter_Prefix() => UI.CurrentRow = null;

	[HarmonyPatch(typeof(Listing_TreeThingFilter), "DoUndiscoveredEntry")]
	[HarmonyPrefix]
	internal static void ListingTreeThingFilter_DoUndiscoveredEntry_Prefix() => UI.CurrentRow = null;

	[HarmonyPatch(typeof(Listing_Lines), "EndLine")]
	[HarmonyPrefix]
	internal static void ListingLines_EndLine_Prefix(Listing_Lines __instance) {
		if (__instance is Listing_TreeThingFilter listing)
			UI.DrawCurrentRow(listing);
		UI.CurrentRow = null;
	}
}