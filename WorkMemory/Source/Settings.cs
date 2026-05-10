using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CaseExtensions;
using RimWorld;
using TrueMogician.Extensions.List;
using TrueMogician.RimWorld.Utility.Attributes;
using TrueMogician.RimWorld.Utility.Extensions;
using TrueMogician.RimWorld.Utility.GUI;
using TrueMogician.RimWorld.WorkMemory.Components;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;

namespace TrueMogician.RimWorld.WorkMemory;

[Translation("WorkMemory.Settings")]
public class Settings : ModSettings {
	private const float _WINDOW_PADDING = 20f;

	private const float _LINE_HEIGHT = 32f;

	private const float _SLIDER_LABEL_WIDTH = 150f;

	private const float _SLIDER_WIDTH = 250f;

	private const float _PREVIEW_HEIGHT = 370f;

	private const int _CHART_SEGMENTS = 160;

	private static readonly Color _chartBackground = new(0.08f, 0.08f, 0.08f, 0.35f);

	private static readonly Color _chartBorder = new(0.45f, 0.45f, 0.45f, 0.55f);

	private static readonly Color _chartGrid = new(1f, 1f, 1f, 0.12f);

	private static readonly Color _chartLine = new(0.42f, 0.72f, 1f, 1f);

	private static readonly Color _chartMarker = new(1f, 0.82f, 0.36f, 0.7f);

	private bool _nonQualityRecipes;

	private float _penalty = WorkMemoryCurve.DEFAULT_PENALTY;

	private float _warmupSpeed = WorkMemoryCurve.DEFAULT_WARMUP_SPEED;

	private int _decayDelay = WorkMemoryCurve.DEFAULT_DECAY_DELAY;

	private float _decaySpeed = WorkMemoryCurve.DEFAULT_DECAY_SPEED;

	private Vector2 _scrollPosition;

	private float _contentHeight = 1f;

	private int _previewWorkAmount = 800;

	private readonly float[] _penaltyChoices = [0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f];

	private readonly float[] _warmupSpeedChoices = [0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f, 2.5f, 3f, 4f];

	private readonly int[] _decayDelayChoices =
		new[] { 0f, 0.5f, 1f, 2f, 3f, 4f, 6f, 8f, 12f, 24f, 36f, 48f, 72f }
			.Select(h => Mathf.RoundToInt(h * GenDate.TicksPerHour))
			.ToArray();

	private readonly float[] _decaySpeedChoices = [0.05f, 0.1f, 0.15f, 0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f];

	private readonly record struct CurvePreview(
		float ReferenceWorkAmount,
		float MomentumCap,
		float NormalTick,
		float FadeStartTick,
		float FadeEndTick
	);

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

	public float MinMultiplier => 1f - _penalty;

	public float MaxMultiplier => 1f + _penalty * 0.5f;

	public void DrawContents(Rect inRect) {
		var outerRect = inRect.Padding(_WINDOW_PADDING / 2, _WINDOW_PADDING);
		var viewRect = new Rect(0f, 0f, outerRect.width - 16f, Mathf.Max(outerRect.height, _contentHeight));
		Widgets.BeginScrollView(outerRect, ref _scrollPosition, viewRect);
		var listing = new Listing_Standard { maxOneColumn = true };
		listing.Begin(viewRect);
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
		listing.Gap(16f);
		DrawCurvePreview(listing);
		_contentHeight = listing.CurHeight + 8f;
		listing.End();
		Widgets.EndScrollView();
	}

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref _nonQualityRecipes, nameof(NonQualityRecipes).ToCamelCase());
		Scribe_Values.Look(ref _penalty, nameof(Penalty).ToCamelCase(), WorkMemoryCurve.DEFAULT_PENALTY);
		Scribe_Values.Look(ref _warmupSpeed, nameof(WarmupSpeed).ToCamelCase(), WorkMemoryCurve.DEFAULT_WARMUP_SPEED);
		Scribe_Values.Look(ref _decayDelay, nameof(DecayDelay).ToCamelCase(), WorkMemoryCurve.DEFAULT_DECAY_DELAY);
		Scribe_Values.Look(ref _decaySpeed, nameof(DecaySpeed).ToCamelCase(), WorkMemoryCurve.DEFAULT_DECAY_SPEED);
	}

	private static void DrawCheckbox(Listing_Standard listing, string memberName, ref bool value) {
		var rect = listing.GetRect(Mathf.Max(Text.LineHeight, _LINE_HEIGHT));
		if (Translate(memberName, "description") is { } tip && !tip.NullOrEmpty())
			TooltipHandler.TipRegion(rect, tip);
		Widgets.CheckboxLabeled(rect, Translate(memberName, "label"), ref value);
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

	private static string FormatTicks(float ticks) {
		int rounded = Mathf.CeilToInt(Mathf.Max(0f, ticks));
		return FormatIllustration("tickFormat", "{0} ticks ({1})", rounded, rounded.ToStringTicksToPeriod());
	}

	private static string FormatAxisTicks(float ticks) {
		int rounded = Mathf.CeilToInt(Mathf.Max(0f, ticks));
		return FormatIllustration("axisTickFormat", "{0} ticks", rounded);
	}

	private static string TranslateIllustration(string subField, string fallback)
		=> $"WorkMemory.Settings.Illustration.{subField}".TryTranslate(out var translation) ? translation : fallback;

	private static string FormatIllustration(string subField, string fallback, params object[] args)
		=> string.Format(TranslateIllustration(subField, fallback), args);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string? Translate(string memberName, string? subField = null)
		=> typeof(Settings).TranslateMember(memberName, subField);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string? FormatSliderLabel(string memberName, params object[] args)
		=> Translate(memberName, "slider.label") is not { } fmt ? null : string.Format(fmt, args);

	private static void DrawPreviewMetric(Rect rect, int index, string label) {
		float columnWidth = rect.width / 2f;
		var row = index / 2;
		var column = index % 2;
		var labelRect = new Rect(rect.x + column * columnWidth, rect.y + row * 24f, columnWidth - 8f, 20f);
		using (new TextBlock(TextAnchor.MiddleLeft))
			Widgets.Label(labelRect, label);
	}

	private static void DrawChartGrid(Rect plotRect) {
		for (var i = 0; i <= 4; i++) {
			float x = Mathf.Lerp(plotRect.xMin, plotRect.xMax, i / 4f);
			float y = Mathf.Lerp(plotRect.yMin, plotRect.yMax, i / 4f);
			Widgets.DrawLine(new Vector2(x, plotRect.yMin), new Vector2(x, plotRect.yMax), _chartGrid, 0.6f);
			Widgets.DrawLine(new Vector2(plotRect.xMin, y), new Vector2(plotRect.xMax, y), _chartGrid, 0.6f);
		}
	}

	private static void DrawMilestone(Rect plotRect, float tick, float xMax, bool drawLabel = true) {
		float x = Mathf.Lerp(plotRect.xMin, plotRect.xMax, Mathf.Clamp01(tick / xMax));
		Widgets.DrawLine(new Vector2(x, plotRect.yMin), new Vector2(x, plotRect.yMax), _chartMarker, 0.8f);
		if (!drawLabel)
			return;
		const float labelWidth = 64f;
		float labelX = Mathf.Clamp(x - labelWidth / 2f, plotRect.xMin, plotRect.xMax - labelWidth);
		using (new TextBlock(GameFont.Tiny, TextAnchor.UpperCenter))
			Widgets.Label(new Rect(labelX, plotRect.yMax + 2f, labelWidth, 16f), FormatAxisTicks(tick));
	}

	private void DrawCurvePreview(Listing_Standard listing) {
		var rect = listing.GetRect(_PREVIEW_HEIGHT);
		Widgets.DrawMenuSection(rect);
		var inner = rect.Padding(12f);
		var rows = inner.ToFlexbox(FlexDirection.Column, [24, _LINE_HEIGHT, 48, "1fr"], 0f, JustifyContent.SpaceBetween).ToArray();

		using (new TextBlock(GameFont.Medium, TextAnchor.MiddleCenter))
			Widgets.Label(rows[0], TranslateIllustration("title", "Curve Preview"));
		var inputRects = rows[1].ToFlexbox([Flexbox.Length.Auto, _SLIDER_LABEL_WIDTH, _SLIDER_WIDTH], 10f).ToArray();
		using (new TextBlock(TextAnchor.MiddleLeft))
			Widgets.Label(inputRects[0], TranslateIllustration("workAmount", "Recipe work amount"));
		using (new TextBlock(TextAnchor.MiddleRight))
			Widgets.Label(inputRects[1], _previewWorkAmount.ToString());
		_previewWorkAmount = WidgetsExtension.HorizontalSlider(
			inputRects[2],
			_previewWorkAmount,
			100,
			2000,
			true,
			step: 100
		);

		var preview = BuildCurvePreview();
		DrawPreviewMetric(rows[2], 0, FormatIllustration("ticksTo100", "100% speed: {0}", FormatTicks(preview.NormalTick)));
		DrawPreviewMetric(rows[2], 1, FormatIllustration("ticksToMaximum", "Maximum speed: {0}", FormatTicks(preview.MomentumCap)));
		DrawPreviewMetric(rows[2], 2, FormatIllustration("ticksToFadeStart", "Starts fading: {0}", FormatTicks(preview.FadeStartTick)));
		DrawPreviewMetric(rows[2], 3, FormatIllustration("ticksToFadeEnd", "Completely faded: {0}", FormatTicks(preview.FadeEndTick)));

		DrawCurveChart(rows[3], preview);
	}

	private CurvePreview BuildCurvePreview() {
		float referenceWorkAmount = WorkMemoryCurve.GetReferenceWorkAmount(_previewWorkAmount, _warmupSpeed);
		float momentumCap = WorkMemoryCurve.GetMomentumCap(referenceWorkAmount);
		float normalTick = WorkMemoryCurve.GetMomentumForMultiplier(1f, referenceWorkAmount, MinMultiplier, MaxMultiplier);
		float fadeStartTick = momentumCap + _decayDelay;
		float fadeEndTick = fadeStartTick + momentumCap / Mathf.Max(_decaySpeed, 0.0001f);
		return new CurvePreview(referenceWorkAmount, momentumCap, normalTick, fadeStartTick, fadeEndTick);
	}

	private void DrawCurveChart(Rect rect, CurvePreview preview) {
		Widgets.DrawBoxSolidWithOutline(rect, _chartBackground, _chartBorder);
		var plotRect = rect.Padding(14f, 14f, 26f, 44f);
		DrawChartGrid(plotRect);
		float xMax = Mathf.Max(1f, preview.FadeEndTick);
		float yMin = MinMultiplier;
		float yMax = MaxMultiplier;
		var prev = GetCurvePoint(0f, plotRect, preview, xMax, yMin, yMax);
		for (var i = 1; i <= _CHART_SEGMENTS; i++) {
			float tick = xMax * i / _CHART_SEGMENTS;
			var current = GetCurvePoint(tick, plotRect, preview, xMax, yMin, yMax);
			Widgets.DrawLine(prev, current, _chartLine, 1.6f);
			prev = current;
		}
		DrawMilestone(plotRect, preview.NormalTick, xMax, false);
		DrawMilestone(plotRect, preview.MomentumCap, xMax);
		DrawMilestone(plotRect, preview.FadeStartTick, xMax);
		DrawMilestone(plotRect, preview.FadeEndTick, xMax);
		DrawChartLabels(rect, plotRect);
	}

	private Vector2 GetCurvePoint(float tick, Rect plotRect, CurvePreview preview, float xMax, float yMin, float yMax) {
		float momentum = tick <= preview.MomentumCap
			? tick
			: tick <= preview.FadeStartTick
				? preview.MomentumCap
				: Mathf.Max(0f, preview.MomentumCap - (tick - preview.FadeStartTick) * _decaySpeed);
		float multiplier = WorkMemoryCurve.GetMultiplier(momentum, preview.ReferenceWorkAmount, MinMultiplier, MaxMultiplier);
		float x = Mathf.Lerp(plotRect.xMin, plotRect.xMax, tick / xMax);
		float y = Mathf.Lerp(plotRect.yMax, plotRect.yMin, Mathf.InverseLerp(yMin, yMax, multiplier));
		return new Vector2(x, y);
	}

	private void DrawChartLabels(Rect rect, Rect plotRect) {
		using (new TextBlock(GameFont.Tiny, TextAnchor.UpperRight)) {
			Widgets.Label(new Rect(rect.x, plotRect.yMin - 3f, 38f, 18f), MaxMultiplier.ToStringPercent());
			Widgets.Label(new Rect(rect.x, plotRect.yMax - 12f, 38f, 18f), MinMultiplier.ToStringPercent());
		}
	}
}