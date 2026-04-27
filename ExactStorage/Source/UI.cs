using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Utility;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage;

using static Formatter;
using static StorageUtility;

[StaticConstructorOnStartup]
public static class UI {
	public const float BASE_WIDTH = 300f;

	public const float BASE_HEIGHT = 480f;

	public const float WIDTH_EXTRA = 120f;

	public const float TOGGLE_HEIGHT = 25f;

	public const float BAR_HEIGHT = 25f;

	public const float CONTROLS_WIDTH = 100f;

	private const float _DIVIDER_HEIGHT = 8f;

	private const float _TOGGLE_MARGIN = 12f;

	private const float _BAR_TOP_PADDING = 6f;

	private const float _FIELD_WIDTH = 40f;

	private const float _FIELD_GAP = 4f;

	private static readonly FieldInfo _curYField = AccessTools.Field(typeof(Listing), "curY");

	private static readonly Color _okColor = new(0.35f, 0.75f, 0.45f);

	private static readonly Color _warnColor = new(0.9f, 0.25f, 0.18f);

	private static readonly Texture2D _okTex = SolidTex(_okColor);

	private static readonly Texture2D _warnTex = SolidTex(_warnColor);

	private static readonly Texture2D _maxTex = SolidTex(new(0.3f, 0.55f, 0.95f));

	private static readonly Texture2D _unknownTex = SolidTex(new(0.6f, 0.6f, 0.6f));

	private static readonly Dictionary<(Quota Quota, bool Min), (string Buffer, bool Focused)> _editState = new();

	public static StorageSettings? CurrentSettings { get; set; }

	public static object? CurrentRow { get; set; }

	public static bool Active => CurrentSettings is { } settings && Enabled(settings);

	public static bool Enabled(StorageSettings settings)
		=> SupportsExactStorage(settings) && Manager.TryGetProfile(settings, out var profile) && profile.Enabled;

	public static float WindowWidth(StorageSettings settings) => Enabled(settings) ? BASE_WIDTH + WIDTH_EXTRA : BASE_WIDTH;

	public static float FooterHeight(StorageSettings? settings) {
		if (settings is null || !SupportsExactStorage(settings))
			return 0f;
		var height = TOGGLE_HEIGHT;
		if (!Enabled(settings))
			return height;
		height += _DIVIDER_HEIGHT + TOGGLE_HEIGHT;
		if (SeparateLinkedStorageAvailable(settings))
			height += TOGGLE_HEIGHT;
		return height + _BAR_TOP_PADDING + BAR_HEIGHT;
	}

	public static void DrawFooter(StorageSettings settings) {
		if (!SupportsExactStorage(settings))
			return;
		var y = BASE_HEIGHT - 3f;
		DrawToggle(settings, y);
		if (!Enabled(settings))
			return;
		y += TOGGLE_HEIGHT;
		Widgets.DrawLineHorizontal(_TOGGLE_MARGIN, y + 3f, WindowWidth(settings) - 2 * _TOGGLE_MARGIN, Widgets.SeparatorLineColor);
		y += _DIVIDER_HEIGHT;
		DrawUnitToggle(settings, y);
		if (SeparateLinkedStorageAvailable(settings)) {
			y += TOGGLE_HEIGHT;
			DrawSeparateToggle(settings, y);
		}
		y += TOGGLE_HEIGHT + _BAR_TOP_PADDING;
		DrawSummaryBar(settings, y);
	}

	public static void DrawCurrentRow(Listing_TreeThingFilter listing) {
		if (CurrentSettings is not { } settings || !Enabled(settings) || !RowAllowed(settings, listing))
			return;
		var profile = Manager.GetProfile(settings);
		var quota = CurrentRow switch {
			ThingDef thingDef           => profile.GetQuota(thingDef, true),
			TreeNode_ThingCategory node => profile.GetQuota(node.catDef, true),
			_                           => null
		};
		if (quota is null)
			return;
		var curY = (float)_curYField.GetValue(listing);
		var row = new Rect(listing.ColumnWidth - CONTROLS_WIDTH, curY, CONTROLS_WIDTH, listing.lineHeight);
		var minRect = new Rect(row.x, row.y, _FIELD_WIDTH, row.height);
		var maxRect = new Rect(minRect.xMax + _FIELD_GAP, row.y, _FIELD_WIDTH, row.height);
		DrawField(minRect, quota, true, settings, profile);
		DrawField(maxRect, quota, false, settings, profile);
	}

	public static void DrawSummaryBar(StorageSettings settings, float y) {
		if (!Enabled(settings))
			return;
		var profile = Manager.GetProfile(settings);
		int min = 0, max = 0;
		var hasMax = false;
		foreach (var quota in profile.Quotas) {
			if (!profile.QuotaUsable(quota) || !QuotaAllowed(settings, quota))
				continue;
			if (quota.HasMin)
				min += AmountUtility.StockSlots(quota.MinStock);
			if (quota.HasMax) {
				max += AmountUtility.StockSlots(quota.MaxStock);
				hasMax = true;
			}
			else if (quota.HasMin) {
				max += AmountUtility.StockSlots(quota.MinStock);
				hasMax = true;
			}
		}
		var rect = new Rect(_TOGGLE_MARGIN, y, WindowWidth(settings) - _TOGGLE_MARGIN * 2, 22f);
		var knownCapacity = TryGetCapacity(settings, out var capacity);
		var warning = knownCapacity && min > capacity;
		var fill = knownCapacity && capacity > 0 ? Mathf.Clamp01((float)min / capacity) : 1f;

		Widgets.DrawMenuSection(rect);
		var bar = rect.ContractedBy(2f);
		GUI.DrawTexture(bar, _unknownTex);
		if (knownCapacity && capacity > 0 && hasMax) {
			var maxBar = bar;
			maxBar.width *= Mathf.Clamp01((float)max / capacity);
			GUI.DrawTexture(maxBar, _maxTex);
		}
		var minBar = bar;
		minBar.width *= fill;
		GUI.DrawTexture(minBar, warning ? _warnTex : knownCapacity ? _okTex : _unknownTex);
		DrawSummaryLabel(bar, min, hasMax, max, knownCapacity, capacity, warning);
		if (Mouse.IsOver(rect)) {
			var tip = warning
				? Translate("SummaryWarning")
				: knownCapacity
					? hasMax
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
		var enabled = profile.Enabled;
		Widgets.CheckboxLabeled(rect, Bold(Translate("Toggle")), ref enabled);
		if (enabled != profile.Enabled) {
			profile.Enabled = enabled;
			NotifyChanged(settings);
		}
		TooltipHandler.TipRegion(rect, Translate("ToggleTip"));
	}

	private static void DrawUnitToggle(StorageSettings settings, float y) {
		var profile = Manager.GetProfile(settings);
		var rect = new Rect(_TOGGLE_MARGIN, y, WindowWidth(settings) - 2 * _TOGGLE_MARGIN, 24f);
		var useStockUnits = profile.UseStockUnits;
		Widgets.CheckboxLabeled(rect, Translate("UseStockUnits"), ref useStockUnits);
		if (useStockUnits != profile.UseStockUnits) {
			profile.UseStockUnits = useStockUnits;
			ClearBuffers();
			NotifyChanged(settings);
		}
		TooltipHandler.TipRegion(rect, Translate("UseStockUnitsTip"));
	}

	private static void DrawSeparateToggle(StorageSettings settings, float y) {
		var profile = Manager.GetProfile(settings);
		var rect = new Rect(_TOGGLE_MARGIN, y, WindowWidth(settings) - 2 * _TOGGLE_MARGIN, 24f);
		var separate = profile.SeparateLinkedStorages;
		Widgets.CheckboxLabeled(rect, Translate("SeparateLinkedStorages"), ref separate);
		if (separate != profile.SeparateLinkedStorages) {
			profile.SeparateLinkedStorages = separate;
			NotifyChanged(settings);
		}
		TooltipHandler.TipRegion(rect, Translate("SeparateLinkedStoragesTip"));
	}

	private static void DrawField(Rect rect, Quota quota, bool min, StorageSettings settings, Profile profile) {
		var key = (quota, min);
		var value = min ? quota.MinStock : quota.MaxStock;
		var control = FieldControlName(quota, min);
		var wasFocused = _editState.TryGetValue(key, out var state) && state.Focused;
		var buffer = wasFocused ? state.Buffer : DisplayValue(profile, quota, value);
		Widgets.DrawHighlightIfMouseover(rect);
		TooltipHandler.TipRegion(
			rect,
			!quota.ValidRange ? Translate("InvalidRangeTip") : min ? Translate("MinTip")
			: Translate("MaxTip")
		);
		var color = GUI.color;
		if (!quota.ValidRange)
			GUI.color = _warnColor;
		GUI.SetNextControlName(control);
		var next = Widgets.TextField(rect, buffer);
		GUI.color = color;
		next = Sanitize(next, profile.UseStockUnits);
		var focused = GUI.GetNameOfFocusedControl() == control;
		if (focused) {
			_editState[key] = (next, true);
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
		var oldValue = min ? quota.MinStock : quota.MaxStock;
		decimal value;
		if (state.Buffer.NullOrEmpty())
			value = AmountUtility.UNSET;
		else if (!TryParseDisplayValue(profile, quota, state.Buffer, out value)) {
			_editState[key] = (DisplayValue(profile, quota, oldValue), false);
			return;
		}
		if (min)
			quota.MinStock = value;
		else
			quota.MaxStock = value;
		var newValue = min ? quota.MinStock : quota.MaxStock;
		_editState[key] = (DisplayValue(profile, quota, newValue), false);
		if (newValue == oldValue)
			return;
		ClearInactive(settings, quota);
		NotifyChanged(settings);
	}

	private static bool RowAllowed(StorageSettings settings, Listing_TreeThingFilter listing) {
		var profile = Manager.GetProfile(settings);
		return CurrentRow switch {
			ThingDef thingDef           => settings.filter.Allows(thingDef),
			TreeNode_ThingCategory node => listing.AllowanceStateOf(node) != MultiCheckboxState.Off && CategoryEditable(profile, node.catDef),
			_                           => false
		};
	}

	private static void DrawSummaryLabel(Rect rect, int min, bool hasMax, int max, bool knownCapacity, int capacity, bool warning) {
		var label = knownCapacity
			? hasMax
				? Translate("Summary", min, max, capacity)
				: Translate("SummaryNoMax", min, capacity)
			: Translate("SummaryUnknownCapacity", min, hasMax ? max.ToStringCached() : "-");
		using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleLeft, false, Color.black)) {
			var width = Text.CalcSize(label).x;
			var x = rect.center.x - width / 2f;
			var labelRect = new Rect(x, rect.y, width, rect.height);
			Widgets.Label(new Rect(labelRect.x + 1f, labelRect.y + 1f, labelRect.width, labelRect.height), label);
			GUI.color = Color.white;
			Widgets.Label(labelRect, label);
		}
	}

	private static string DisplayValue(Profile profile, Quota quota, decimal stock) {
		if (stock < 0m)
			return string.Empty;
		if (profile.UseStockUnits)
			return AmountUtility.Format(stock);
		if (!TryGetRawStackLimit(quota, out var stackLimit))
			return string.Empty;
		var raw = Math.Round(stock * stackLimit, 0, MidpointRounding.AwayFromZero);
		return raw.ToString(CultureInfo.InvariantCulture);
	}

	private static bool TryParseDisplayValue(Profile profile, Quota quota, string text, out decimal stock) {
		stock = AmountUtility.UNSET;
		if (profile.UseStockUnits) {
			if (!AmountUtility.TryParse(text, out stock))
				return false;
			stock = AmountUtility.Normalize(stock);
			return true;
		}
		if (!TryGetRawStackLimit(quota, out var stackLimit) || !int.TryParse(text, out var raw))
			return false;
		stock = AmountUtility.RawToStock(raw, stackLimit);
		return true;
	}

	private static bool TryGetRawStackLimit(Quota quota, out int stackLimit) {
		if (quota.ThingDef is { } thingDef) {
			stackLimit = DefCache.StackLimitOf(thingDef);
			return true;
		}
		if (quota.CategoryDef is { } categoryDef)
			return DefCache.TryGetUnifiedStackLimit(categoryDef, out stackLimit);
		stackLimit = 0;
		return false;
	}

	private static bool CategoryEditable(Profile profile, ThingCategoryDef categoryDef)
		=> profile.UseStockUnits || DefCache.TryGetUnifiedStackLimit(categoryDef, out _);

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