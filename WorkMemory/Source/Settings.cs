using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CaseExtensions;
using RimWorld;
using TrueMogician.Extensions.List;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;
using TrueMogician.RimWorld.Utility.Extensions;
using TrueMogician.RimWorld.Utility.GUI;
using TrueMogician.RimWorld.WorkMemory.Components;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.WorkMemory;

[Translation("WorkMemory.Settings")]
public enum KeyMode : byte {
	Item,

	Category,

	Workbench
}

[Translation("WorkMemory.Settings")]
public class Settings : ModSettings {
	private const float _WINDOW_PADDING = 20f;

	private const float _LINE_HEIGHT = 32f;

	private const float _SLIDER_LABEL_WIDTH = 150f;

	private const float _SLIDER_WIDTH = 250f;

	private KeyMode _keyMode = KeyMode.Category;

	private KeyMode _savedKeyMode = KeyMode.Category;

	private bool _nonQualityRecipes;

	private float _penalty = WorkMemoryCurve.DEFAULT_PENALTY;

	private float _warmupSpeed = WorkMemoryCurve.DEFAULT_WARMUP_SPEED;

	private int _decayDelay = WorkMemoryCurve.DEFAULT_DECAY_DELAY;

	private float _decaySpeed = WorkMemoryCurve.DEFAULT_DECAY_SPEED;

	private readonly float[] _penaltyChoices = [0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f];

	private readonly float[] _warmupSpeedChoices = [0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f, 2.5f, 3f, 4f];

	private readonly int[] _decayDelayChoices =
		new[] { 0f, 0.5f, 1f, 2f, 3f, 4f, 6f, 8f, 12f, 24f, 36f, 48f, 72f }
			.Select(h => Mathf.RoundToInt(h * GenDate.TicksPerHour))
			.ToArray();

	private readonly float[] _decaySpeedChoices = [0.05f, 0.1f, 0.15f, 0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f];

	public static Settings Default { get; internal set; } = null!;

	[Translation]
	public bool NonQualityRecipes => _nonQualityRecipes;

	[Translation]
	public float Penalty => _penalty;

	[Translation]
	public float WarmupSpeed => _warmupSpeed;

	[Translation]
	public int DecayDelay => _decayDelay;

	[Translation]
	public float DecaySpeed => _decaySpeed;

	[Translation]
	public KeyMode KeyMode => _keyMode;

	public float MinMultiplier => 1f - _penalty;

	public float MaxMultiplier => 1f + _penalty * 0.5f;

	public void DrawContents(Rect inRect) {
		var listing = new Listing_Standard();
		listing.Begin(inRect.Padding(_WINDOW_PADDING));
		DrawEnum(listing, nameof(KeyMode), () => _keyMode, mode => _keyMode = mode);
		DrawCheckbox(listing, nameof(NonQualityRecipes), ref _nonQualityRecipes);
		DrawSlider(
			listing,
			nameof(Penalty),
			ref _penalty,
			_penaltyChoices,
			v => [v.ToStringPercent(), (v / 2).ToStringPercent()]
		);
		DrawSlider(listing, nameof(WarmupSpeed), ref _warmupSpeed, _warmupSpeedChoices, v => [v.ToString("0.##")]);
		DrawSlider(
			listing,
			nameof(DecayDelay),
			ref _decayDelay,
			_decayDelayChoices,
			v => [((float)v / GenDate.TicksPerHour).ToString("0.##")]
		);
		DrawSlider(listing, nameof(DecaySpeed), ref _decaySpeed, _decaySpeedChoices, v => [v.ToString("0.##")]);
		listing.End();
	}

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref _keyMode, nameof(KeyMode).ToCamelCase());
		Scribe_Values.Look(ref _nonQualityRecipes, nameof(NonQualityRecipes).ToCamelCase());
		Scribe_Values.Look(ref _penalty, nameof(Penalty).ToCamelCase(), WorkMemoryCurve.DEFAULT_PENALTY);
		Scribe_Values.Look(ref _warmupSpeed, nameof(WarmupSpeed).ToCamelCase(), WorkMemoryCurve.DEFAULT_WARMUP_SPEED);
		Scribe_Values.Look(ref _decayDelay, nameof(DecayDelay).ToCamelCase(), WorkMemoryCurve.DEFAULT_DECAY_DELAY);
		Scribe_Values.Look(ref _decaySpeed, nameof(DecaySpeed).ToCamelCase(), WorkMemoryCurve.DEFAULT_DECAY_SPEED);
		if (Scribe.mode == LoadSaveMode.LoadingVars)
			_savedKeyMode = _keyMode;
		else if (Scribe.mode == LoadSaveMode.Saving && _savedKeyMode != _keyMode) {
			CachedGameComponent<WorkMemoryComponent>.TryGet()?.ClearRecords();
			_savedKeyMode = _keyMode;
		}
	}

	private static void DrawCheckbox(Listing_Standard listing, string memberName, ref bool value) {
		var rect = listing.GetRect(Mathf.Max(Text.LineHeight, _LINE_HEIGHT));
		if (Translate(memberName, "description") is { } tip && !tip.NullOrEmpty())
			TooltipHandler.TipRegion(rect, tip);
		Widgets.CheckboxLabeled(rect, Translate(memberName, "label"), ref value);
	}

	private static void DrawEnum<T>(
		Listing_Standard listing,
		string memberName,
		Func<T> getValue,
		Action<T> setValue
	) where T : struct {
		var value = getValue();
		var rects = GetRowRects(listing);
		if (Translate(memberName, "description") is { } tip && !tip.NullOrEmpty())
			TooltipHandler.TipRegion(rects[0], tip);
		using (new TextBlock(TextAnchor.MiddleLeft))
			Widgets.Label(rects[0], Translate(memberName, "label"));
		if (Widgets.ButtonText(rects[2], Translate(memberName, value.ToString()) ?? value.ToString())) {
			var options = Enum.GetValues(typeof(T))
				.Cast<T>()
				.Select(mode => new FloatMenuOption(
						Translate(memberName, mode.ToString()) ?? mode.ToString(),
						() => setValue(mode)
					)
				)
				.ToList();
			Find.WindowStack.Add(new FloatMenu(options));
		}
	}

	private static void DrawSlider<T>(
		Listing_Standard listing,
		string memberName,
		ref T value,
		IReadOnlyList<T> choices,
		Func<T, object[]> formatArgsSelector
	) where T : notnull {
		var rects = GetRowRects(listing);
		if (Translate(memberName, "description") is { } tip && !tip.NullOrEmpty())
			TooltipHandler.TipRegion(rects[0], tip);
		using (new TextBlock(TextAnchor.MiddleLeft))
			Widgets.Label(rects[0], Translate(memberName, "label"));
		using (new TextBlock(TextAnchor.MiddleRight))
			Widgets.Label(rects[1], FormatLabel(value));
		int index = Math.Clamp(choices.BinarySearch(value), 0, choices.Count - 1);
		value = WidgetsExtension.HorizontalSlider(rects[2], index, choices, FormatLabel, true, false);
		return;
		string FormatLabel(T v) => FormatSliderLabel(memberName, formatArgsSelector(v)) ?? v.ToString();
	}

	private static Rect[] GetRowRects(Listing_Standard listing) => listing.GetRect(Mathf.Max(Text.LineHeight, _LINE_HEIGHT))
		.ToFlexbox([Flexbox.Length.Auto, _SLIDER_LABEL_WIDTH, _SLIDER_WIDTH], 10f)
		.ToArray();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string? Translate(string memberName, string? subField = null)
		=> typeof(Settings).TranslateMember(memberName, subField);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string? FormatSliderLabel(string memberName, params object[] args)
		=> Translate(memberName, "slider.label") is not { } fmt ? null : string.Format(fmt, args);
}