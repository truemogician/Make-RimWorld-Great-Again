using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using TrueMogician.Extensions.Enumerable;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Extensions;
using TrueMogician.RimWorld.Utility.GUI;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage;

using static AmountUtility;
using static Formatter;

[StaticConstructorOnStartup]
public static class UI {
	public const float BASE_WIDTH = 280f;

	public const float BASE_HEIGHT = 480f;

	public const float TOGGLE_HEIGHT = 25f;

	public const float BAR_HEIGHT = 25f;

	public const float CONTROLS_WIDTH = 100f;

	private const float _DIVIDER_HEIGHT = 8f;

	private const float _TOGGLE_MARGIN = 12f;

	private const float _BAR_TOP_PADDING = 6f;

	private const float _FIELD_WIDTH = 40f;

	private const float _CONTROLS_WIDTH = _FIELD_WIDTH * 2 + 20f;

	private static readonly Color _okColor = new(0.35f, 0.75f, 0.45f);

	private static readonly Color _warnColor = new(0.9f, 0.25f, 0.18f);

	private static readonly Color _pendingColor = new(0.55f, 0.55f, 0.55f);

	private static readonly Texture2D _okTex = SolidTex(_okColor);

	private static readonly Texture2D _warnTex = SolidTex(_warnColor);

	private static readonly Texture2D _maxTex = SolidTex(new(0.3f, 0.55f, 0.95f));

	private static readonly Texture2D _unknownTex = SolidTex(new(0.6f, 0.6f, 0.6f));

	private static readonly Dictionary<(Quota Quota, bool Min), (string Buffer, bool Focused)> _editState = new();

	private static readonly FieldInfo _listingCurY = AccessTools.Field(typeof(Listing), "curY");

	public static StorageSettings? CurrentSettings { get; set; }

	public static object? CurrentRow { get; set; }

	public static bool Active => CurrentSettings is { ExactStorageEnabled: true };

	public static float WindowWidth(StorageSettings settings) => settings.ExactStorageEnabled ? BASE_WIDTH + _CONTROLS_WIDTH : BASE_WIDTH;

	public static float FooterHeight(StorageSettings? settings) {
		if (settings is null || !settings.SupportsExactStorage)
			return 0f;
		float height = TOGGLE_HEIGHT;
		if (!settings.ExactStorageEnabled)
			return height;
		height += _DIVIDER_HEIGHT + TOGGLE_HEIGHT;
		if (settings.SeparateLinkedStorageAvailable)
			height += TOGGLE_HEIGHT;
		return height + _BAR_TOP_PADDING + BAR_HEIGHT;
	}

	public static void DrawFooter(StorageSettings settings) {
		if (!settings.SupportsExactStorage)
			return;
		float y = BASE_HEIGHT - 3f;
		DrawToggle(settings, y);
		if (!settings.ExactStorageEnabled)
			return;
		y += TOGGLE_HEIGHT;
		Widgets.DrawLineHorizontal(_TOGGLE_MARGIN, y + 3f, WindowWidth(settings) - 2 * _TOGGLE_MARGIN, Widgets.SeparatorLineColor);
		y += _DIVIDER_HEIGHT;
		DrawUnitToggle(settings, y);
		if (settings.SeparateLinkedStorageAvailable) {
			y += TOGGLE_HEIGHT;
			DrawSeparateToggle(settings, y);
		}
		y += TOGGLE_HEIGHT + _BAR_TOP_PADDING;
		DrawSummaryBar(settings, y);
	}

	public static void DrawCurrentRow(Listing_TreeThingFilter listing) {
		if (CurrentSettings is not { ExactStorageEnabled: true } settings || !RowAllowed(settings, listing))
			return;
		var profile = Manager.GetProfile(settings);
		Quota? quota = CurrentRow switch {
			ThingDef thingDef           => profile.GetOrCreateQuota(thingDef),
			TreeNode_ThingCategory node => profile.GetOrCreateQuota(node.catDef),
			_                           => null
		};
		if (quota is null)
			return;
		var rects = new Rect(listing.ColumnWidth - CONTROLS_WIDTH, (float)_listingCurY.GetValue(listing), CONTROLS_WIDTH, listing.lineHeight)
			.Padding(0f, 10f, 0f, 5f)
			.ToFlexbox([_FIELD_WIDTH, _FIELD_WIDTH], 0f, JustifyContent.SpaceBetween)
			.ToArray();
		DrawField(rects[0], quota, true, settings, profile);
		DrawField(rects[1], quota, false, settings, profile);
	}

	public static void DrawSummaryBar(StorageSettings settings, float y) {
		if (!settings.ExactStorageEnabled)
			return;
		var profile = Manager.GetProfile(settings);
		uint min = DefCache.RootCategoryDefs.Sum(c => profile.CategoryChildrenSlots(c, false));
		uint max = DefCache.RootCategoryDefs.Sum(c => profile.CategoryChildrenSlots(c, true));
		var rect = new Rect(_TOGGLE_MARGIN, y, WindowWidth(settings) - _TOGGLE_MARGIN * 2, 22f);
		bool knownCapacity = settings.TryGetCapacity(out int capacity);
		bool warning = knownCapacity && min > (uint)Math.Max(0, capacity);
		float fill = knownCapacity && capacity > 0 ? Mathf.Clamp01((float)min / capacity) : 1f;

		Widgets.DrawMenuSection(rect);
		var bar = rect.ContractedBy(2f);
		GUI.DrawTexture(bar, _unknownTex);
		if (knownCapacity && capacity > 0 && max != 0u) {
			var maxBar = bar;
			maxBar.width *= Mathf.Clamp01((float)max / capacity);
			GUI.DrawTexture(maxBar, _maxTex);
		}
		var minBar = bar;
		minBar.width *= fill;
		GUI.DrawTexture(minBar, warning ? _warnTex : knownCapacity ? _okTex : _unknownTex);
		DrawSummaryLabel(bar, min, max, knownCapacity, capacity);
		if (Mouse.IsOver(rect)) {
			string tip = warning
				? Translate("SummaryWarning")
				: knownCapacity
					? max != 0u
						? Translate("Summary", min, max, capacity)
						: Translate("SummaryNoMax", min, capacity)
					: Translate("SummaryUnknownCapacityWarning");
			TooltipHandler.TipRegion(rect, tip);
		}
	}

	public static void ClearInactive(StorageSettings settings, Quota quota) {
		if (quota.Active)
			return;
		Manager.GetProfile(settings).PruneInactive();
	}

	private static void DrawToggle(StorageSettings settings, float y) {
		var profile = Manager.GetProfile(settings);
		var rect = new Rect(_TOGGLE_MARGIN, y, WindowWidth(settings) - 2 * _TOGGLE_MARGIN, 24f);
		bool enabled = profile.Enabled;
		Widgets.CheckboxLabeled(rect, Bold(Translate("Toggle")), ref enabled);
		if (enabled != profile.Enabled) {
			profile.Enabled = enabled;
			settings.NotifyChanged();
		}
		TooltipHandler.TipRegion(rect, Translate("ToggleTip"));
	}

	private static void DrawUnitToggle(StorageSettings settings, float y) {
		var profile = Manager.GetProfile(settings);
		var rect = new Rect(_TOGGLE_MARGIN, y, WindowWidth(settings) - 2 * _TOGGLE_MARGIN, 24f);
		bool useStackUnit = profile.UseStackUnit;
		Widgets.CheckboxLabeled(rect, Translate("UseStackUnit"), ref useStackUnit);
		if (useStackUnit != profile.UseStackUnit) {
			profile.UseStackUnit = useStackUnit;
			ClearBuffers();
			settings.NotifyChanged();
		}
		TooltipHandler.TipRegion(rect, Translate("UseStackUnitTip"));
	}

	private static void DrawSeparateToggle(StorageSettings settings, float y) {
		var profile = Manager.GetProfile(settings);
		var rect = new Rect(_TOGGLE_MARGIN, y, WindowWidth(settings) - 2 * _TOGGLE_MARGIN, 24f);
		bool separate = profile.SeparateLinkedStorages;
		Widgets.CheckboxLabeled(rect, Translate("SeparateLinkedStorages"), ref separate);
		if (separate != profile.SeparateLinkedStorages) {
			profile.SeparateLinkedStorages = separate;
			settings.NotifyChanged();
		}
		TooltipHandler.TipRegion(rect, Translate("SeparateLinkedStoragesTip"));
	}

	private static void DrawField(Rect rect, Quota quota, bool min, StorageSettings settings, Profile profile) {
		var key = (quota, min);
		decimal value = min ? quota.Min : quota.Max;
		string control = FieldControlName(quota, min);
		string committed = DisplayValue(profile, quota, value);
		bool wasFocused = _editState.TryGetValue(key, out var state) && state.Focused;
		string? buffer = wasFocused ? state.Buffer : committed;
		bool pending = wasFocused && state.Buffer != committed;
		bool invalidRange = quota is { Active: true, ValidRange: false };
		bool invalidCategoryTotal = quota is { Active: true, ValidRange: true } && !profile.CategoryTotalsValid(quota);
		bool invalid = invalidRange || invalidCategoryTotal;
		Widgets.DrawHighlightIfMouseover(rect);
		TooltipHandler.TipRegion(
			rect,
			invalidRange ? Translate("InvalidRangeTip")
			: invalidCategoryTotal ? Translate("InvalidCategoryTotalTip") : min ? Translate("MinTip")
			: Translate("MaxTip")
		);
		var color = GUI.color;
		if (invalid)
			GUI.color = _warnColor;
		else if (pending)
			GUI.color = _pendingColor;
		bool enter = GUI.GetNameOfFocusedControl() == control
			&& Event.current.type == EventType.KeyDown
			&& Event.current.keyCode is KeyCode.Return or KeyCode.KeypadEnter;
		GUI.SetNextControlName(control);
		string? next = Widgets.TextField(rect, buffer);
		GUI.color = color;
		next = Sanitize(next, profile.UseStackUnit);
		bool focused = GUI.GetNameOfFocusedControl() == control;
		if (focused) {
			_editState[key] = (next, true);
			if (enter) {
				CommitField(quota, min, settings, profile);
				GUIUtility.keyboardControl = 0;
				if (Event.current.type == EventType.KeyDown)
					Event.current.Use();
			}
			return;
		}
		if (!wasFocused)
			return;
		_editState[key] = (next, false);
		CommitField(quota, min, settings, profile);
	}

	private static void CommitField(Quota quota, bool min, StorageSettings settings, Profile profile) {
		var key = (quota, min);
		if (!_editState.TryGetValue(key, out var state))
			return;
		decimal oldValue = min ? quota.Min : quota.Max;
		decimal value;
		if (state.Buffer.NullOrEmpty())
			value = UNSET;
		else if (!TryParseDisplayValue(profile, quota, state.Buffer, out value)) {
			_editState[key] = (DisplayValue(profile, quota, oldValue), false);
			return;
		}
		if (min)
			quota.Min = value;
		else
			quota.Max = value;
		decimal newValue = min ? quota.Min : quota.Max;
		_editState[key] = (DisplayValue(profile, quota, newValue), false);
		if (newValue == oldValue)
			return;
		ClearInactive(settings, quota);
		settings.NotifyChanged();
	}

	private static bool RowAllowed(StorageSettings settings, Listing_TreeThingFilter listing) {
		var profile = Manager.GetProfile(settings);
		return CurrentRow switch {
			ThingDef thingDef           => settings.filter.Allows(thingDef),
			TreeNode_ThingCategory node => listing.AllowanceStateOf(node) != MultiCheckboxState.Off && CategoryEditable(profile, node.catDef),
			_                           => false
		};
	}

	private static void DrawSummaryLabel(Rect rect, uint min, uint max, bool knownCapacity, int capacity) {
		string label = knownCapacity
			? max != 0u
				? Translate("Summary", min, max, capacity)
				: Translate("SummaryNoMax", min, capacity)
			: Translate("SummaryUnknownCapacity", min, max != 0u ? max.ToString(CultureInfo.InvariantCulture) : "-");
		using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleLeft, false, Color.black)) {
			float width = Text.CalcSize(label).x;
			float x = rect.center.x - width / 2f;
			var labelRect = new Rect(x, rect.y, width, rect.height);
			Widgets.Label(new Rect(labelRect.x + 1f, labelRect.y + 1f, labelRect.width, labelRect.height), label);
			GUI.color = Color.white;
			Widgets.Label(labelRect, label);
		}
	}

	private static string DisplayValue(Profile profile, Quota quota, decimal stack) {
		if (stack < 0m)
			return string.Empty;
		if (profile.UseStackUnit)
			return Format(stack);
		if (!TryGetRawStackLimit(quota, out int stackLimit))
			return string.Empty;
		decimal raw = Math.Round(stack * stackLimit, 0, MidpointRounding.AwayFromZero);
		return raw.ToString(CultureInfo.InvariantCulture);
	}

	private static bool TryParseDisplayValue(Profile profile, Quota quota, string text, out decimal stack) {
		stack = UNSET;
		if (profile.UseStackUnit) {
			if (!TryParse(text, out stack))
				return false;
			stack = Normalize(stack);
			return true;
		}
		if (!TryGetRawStackLimit(quota, out int stackLimit) || !int.TryParse(text, out int raw))
			return false;
		stack = RawToStack(raw, stackLimit);
		return true;
	}

	private static bool TryGetRawStackLimit(Quota quota, out int stackLimit) {
		switch (quota) {
			case ThingQuota { ThingDef: { } thingDef }:
				stackLimit = Math.Max(1, thingDef.stackLimit);
				return true;
			case ThingCategoryQuota { CategoryDef: { } categoryDef }: return DefCache.TryGetUnifiedStackLimit(categoryDef, out stackLimit);
			default:
				stackLimit = 0;
				return false;
		}
	}

	private static bool CategoryEditable(Profile profile, ThingCategoryDef categoryDef) =>
		profile.UseStackUnit || DefCache.TryGetUnifiedStackLimit(categoryDef, out _);

	private static string Translate(string key, params NamedArgument[] args) => $"{nameof(ExactStorage)}.{nameof(UI)}.{key}".Translate(args);

	private static string FieldControlName(Quota quota, bool min) => $"{nameof(ExactStorage)}.{quota.Key}.{(min ? "Min" : "Max")}";

	private static void ClearBuffers() => _editState.Clear();

	private static string Sanitize(string text, bool allowDecimal) {
		if (text.NullOrEmpty())
			return string.Empty;
		var chars = new char[text.Length];
		var count = 0;
		var decimalUsed = false;
		foreach (char ch in text) {
			if (char.IsDigit(ch)) {
				chars[count++] = ch;
				continue;
			}
			if (allowDecimal && ch == '.' && !decimalUsed) {
				chars[count++] = ch;
				decimalUsed = true;
			}
		}
		return count == 0 ? string.Empty : new string(chars, 0, count);
	}

	private static Texture2D SolidTex(Color color) {
		var tex = new Texture2D(1, 1);
		tex.SetPixel(0, 0, color);
		tex.Apply();
		return tex;
	}
}