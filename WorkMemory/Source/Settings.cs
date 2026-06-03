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

	private float _permanentScale = WorkMemoryCurve.DEFAULT_PERMANENT_SCALE;

	private float _permanentCurvature = WorkMemoryCurve.DEFAULT_PERMANENT_CURVATURE;

	private float _permanentMaxFraction = WorkMemoryCurve.DEFAULT_PERMANENT_MAX_FRACTION;

	private SettingsTab _selectedTab;

	private readonly List<TabRecord> _tabs = [];

	private float _workAmount = 1000f;

	private readonly float[] _penaltyChoices = [0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f];

	private readonly float[] _warmupSpeedChoices = [0.2f, 1f / 3, 0.5f, 0.75f, 1f, 1.5f, 2f, 2.5f, 3f, 4f, 5f];

	private readonly int[] _decayDelayChoices = new[] { 0, 1, 2, 4, 6, 8, 12, 24, 36, 48, 72 }.Select(h => h * GenDate.TicksPerHour).ToArray();

	private readonly float[] _decaySpeedChoices = [0.1f, 0.2f, 0.25f, 1f / 3, 0.5f, 0.75f, 1f, 1.5f, 2f, 3f];

	private readonly float[] _permanentScaleChoices = [1f, 2f, 3f, 4f, 6f, 8f, 12f, 16f, 24f, 32f];

	private readonly float[] _permanentCurvatureChoices = [0.25f, 1f / 3, 0.5f, 0.75f, 1f, 1.5f, 2f];

	private readonly float[] _permanentMaxFractionChoices = [0f, 0.25f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f];

	private readonly float[] _workAmountChoices = [200f, 400f, 600f, 800f, 1000f, 1500f, 2000f, 3000f, 4000f, 6000f, 8000f];

	private enum SettingsTab : byte {
		General,
		TransientCurve,
		PermanentCurve
	}

	private readonly record struct CurvePreview(
		float ReferenceWorkAmount,
		float MomentumCap,
		float NormalTick,
		float FadeStartTick,
		float FadeEndTick
	);

	private readonly record struct PermanentPreview(float NeutralReps, float MasteryReps);

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
	public float PermanentScale => _permanentScale;

	[Translation]
	public float PermanentCurvature => _permanentCurvature;

	[Translation]
	public float PermanentMaxFraction => _permanentMaxFraction;

	public float MinMultiplier => 1f - _penalty;

	public float MaxMultiplier => 1f + _penalty * 0.5f;

	public void DrawContents(Rect inRect) {
		if (_tabs.Count == 0)
			BuildTabs();
		var tabBase = inRect;
		tabBase.yMin += TabDrawer.TabHeight;
		Widgets.DrawMenuSection(tabBase);
		TabDrawer.DrawTabs(tabBase, _tabs);
		var contentRect = tabBase.Padding(_WINDOW_PADDING / 2, _WINDOW_PADDING);
		var listing = new Listing_Standard { maxOneColumn = true };
		listing.Begin(contentRect);
		switch (_selectedTab) {
			case SettingsTab.General:        DrawGeneralTab(listing); break;
			case SettingsTab.TransientCurve: DrawTransientTab(listing); break;
			case SettingsTab.PermanentCurve: DrawPermanentTab(listing); break;
		}
		listing.End();
	}

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref _nonQualityRecipes, nameof(NonQualityRecipes).ToCamelCase());
		Scribe_Values.Look(ref _penalty, nameof(Penalty).ToCamelCase(), WorkMemoryCurve.DEFAULT_PENALTY);
		Scribe_Values.Look(ref _warmupSpeed, nameof(WarmupSpeed).ToCamelCase(), WorkMemoryCurve.DEFAULT_WARMUP_SPEED);
		Scribe_Values.Look(ref _decayDelay, nameof(DecayDelay).ToCamelCase(), WorkMemoryCurve.DEFAULT_DECAY_DELAY);
		Scribe_Values.Look(ref _decaySpeed, nameof(DecaySpeed).ToCamelCase(), WorkMemoryCurve.DEFAULT_DECAY_SPEED);
		Scribe_Values.Look(ref _permanentScale, nameof(PermanentScale).ToCamelCase(), WorkMemoryCurve.DEFAULT_PERMANENT_SCALE);
		Scribe_Values.Look(ref _permanentCurvature, nameof(PermanentCurvature).ToCamelCase(), WorkMemoryCurve.DEFAULT_PERMANENT_CURVATURE);
		Scribe_Values.Look(ref _permanentMaxFraction, nameof(PermanentMaxFraction).ToCamelCase(), WorkMemoryCurve.DEFAULT_PERMANENT_MAX_FRACTION);
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string TranslateKey(string subField) => $"WorkMemory.Settings.{subField}".Translate();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string FormatKey(string subField, params object[] args) => string.Format(TranslateKey(subField), args);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string? Translate(string memberName, string? subField = null) => typeof(Settings).TranslateMember(memberName, subField);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string? FormatSliderLabel(string memberName, params object[] args) =>
		Translate(memberName, "slider.label") is not { } fmt ? null : string.Format(fmt, args);

	private static string FormatTicks(float ticks) {
		int rounded = Mathf.CeilToInt(Mathf.Max(0f, ticks));
		return FormatKey("TransientChart.tickFormat", rounded, rounded.ToStringTicksToPeriod());
	}

	private static string FormatAxisTicks(float ticks) {
		int rounded = Mathf.CeilToInt(Mathf.Max(0f, ticks));
		return FormatKey("TransientChart.axisTickFormat", rounded);
	}

	private static string FormatReps(float reps) {
		if (reps < 0f)
			return TranslateKey("PermanentChart.unreachable");
		return FormatKey("PermanentChart.workFormat", Mathf.CeilToInt(Mathf.Max(0f, reps)));
	}

	private static void DrawPreviewMetric(Rect rect, int index, string label) {
		float columnWidth = rect.width / 2f;
		var row = index / 2;
		var column = index % 2;
		var labelRect = new Rect(rect.x + column * columnWidth, rect.y + row * 24f, columnWidth - 8f, 20f);
		using (new TextBlock(TextAnchor.MiddleLeft))
			Widgets.Label(labelRect, label);
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

	private static void DrawRepsMilestone(Rect plotRect, float reps, float xMax) {
		float x = Mathf.Lerp(plotRect.xMin, plotRect.xMax, Mathf.Clamp01(reps / xMax));
		Widgets.DrawLine(new Vector2(x, plotRect.yMin), new Vector2(x, plotRect.yMax), _chartMarker, 0.8f);
		const float labelWidth = 72f;
		float labelX = Mathf.Clamp(x - labelWidth / 2f, plotRect.xMin, plotRect.xMax - labelWidth);
		using (new TextBlock(GameFont.Tiny, TextAnchor.UpperCenter))
			Widgets.Label(new Rect(labelX, plotRect.yMax + 2f, labelWidth, 16f), FormatReps(reps));
	}

	private void BuildTabs() {
		foreach (SettingsTab tab in Enum.GetValues(typeof(SettingsTab))) {
			_tabs.Add(
				new TabRecord(
					TranslateKey($"Tab.{tab}"),
					() => _selectedTab = tab,
					() => _selectedTab == tab
				)
			);
		}
	}

	private void DrawGeneralTab(Listing_Standard listing) {
		DrawCheckbox(listing, nameof(NonQualityRecipes), ref _nonQualityRecipes);
		DrawSlider(
			listing,
			nameof(Penalty),
			ref _penalty,
			_penaltyChoices,
			v => [v.ToStringPercent(), (v / 2).ToStringPercent()]
		);
	}

	private void DrawTransientTab(Listing_Standard listing) {
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
	}

	private void DrawPermanentTab(Listing_Standard listing) {
		DrawSlider(listing, nameof(PermanentScale), ref _permanentScale, _permanentScaleChoices, v => [v.ToString("0.##")]);
		DrawSlider(listing, nameof(PermanentCurvature), ref _permanentCurvature, _permanentCurvatureChoices, v => [v.ToString("0.##")]);
		DrawSlider(
			listing,
			nameof(PermanentMaxFraction),
			ref _permanentMaxFraction,
			_permanentMaxFractionChoices,
			v => [v <= 0f ? TranslateKey($"{nameof(PermanentMaxFraction)}.disabled") : PermanentFractionToMultiplier(v).ToStringPercent()]
		);
		listing.Gap(16f);
		DrawPermanentPreview(listing);
	}

	private float PermanentFractionToMultiplier(float fraction) =>
		WorkMemoryCurve.GetMultiplier(
			WorkMemoryCurve.GetMomentumCap(WorkMemoryCurve.MIN_REFERENCE_WORK_AMOUNT) * fraction,
			WorkMemoryCurve.MIN_REFERENCE_WORK_AMOUNT,
			MinMultiplier,
			MaxMultiplier
		);

	private void DrawChartGrid(Rect plotRect) {
		for (var i = 0; i <= 4; i++) {
			float x = Mathf.Lerp(plotRect.xMin, plotRect.xMax, i / 4f);
			Widgets.DrawLine(new Vector2(x, plotRect.yMin), new Vector2(x, plotRect.yMax), _chartGrid, 0.6f);
		}
		foreach (float y in GetHorizontalGridLines(plotRect))
			Widgets.DrawLine(new Vector2(plotRect.xMin, y), new Vector2(plotRect.xMax, y), _chartGrid, 0.6f);
	}

	private IEnumerable<float> GetHorizontalGridLines(Rect plotRect) {
		yield return plotRect.yMin;
		yield return Mathf.Lerp(plotRect.yMax, plotRect.yMin, Mathf.InverseLerp(MinMultiplier, MaxMultiplier, 1f));
		yield return plotRect.yMax;
	}

	private void DrawCurvePreview(Listing_Standard listing) {
		var rect = listing.GetRect(_PREVIEW_HEIGHT);
		Widgets.DrawMenuSection(rect);
		var inner = rect.Padding(12f);
		var rows = inner.ToFlexbox(FlexDirection.Column, [24, _LINE_HEIGHT, 48, "1fr"], 4f, JustifyContent.SpaceBetween).ToArray();

		using (new TextBlock(GameFont.Medium, TextAnchor.MiddleCenter))
			Widgets.Label(rows[0], TranslateKey("TransientChart.title"));
		var cols = rows[1].ToFlexbox([Flexbox.Length.Auto, _SLIDER_LABEL_WIDTH, _SLIDER_WIDTH], 10f).ToArray();
		using (new TextBlock(TextAnchor.MiddleLeft))
			Widgets.Label(cols[0], TranslateKey("TransientChart.workAmount"));
		using (new TextBlock(TextAnchor.MiddleRight))
			Widgets.Label(cols[1], _workAmount.ToString("F0"));
		_workAmount = WidgetsExtension.HorizontalSlider(cols[2], _workAmount, _workAmountChoices, v => v.ToString("F0"), true, false);

		var preview = BuildCurvePreview();
		DrawPreviewMetric(rows[2], 0, FormatKey("TransientChart.ticksTo100", FormatTicks(preview.NormalTick)));
		DrawPreviewMetric(rows[2], 1, FormatKey("TransientChart.ticksToMaximum", FormatTicks(preview.MomentumCap)));
		DrawPreviewMetric(rows[2], 2, FormatKey("TransientChart.ticksToFadeStart", FormatTicks(preview.FadeStartTick)));
		DrawPreviewMetric(rows[2], 3, FormatKey("TransientChart.ticksToFadeEnd", FormatTicks(preview.FadeEndTick)));

		DrawCurveChart(rows[3], preview);
	}

	private CurvePreview BuildCurvePreview() {
		float referenceWorkAmount = WorkMemoryCurve.GetReferenceWorkAmount(_workAmount, _warmupSpeed);
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
			float normalY = Mathf.Lerp(plotRect.yMax, plotRect.yMin, Mathf.InverseLerp(MinMultiplier, MaxMultiplier, 1f));
			Widgets.Label(new Rect(rect.x, normalY - 9f, 38f, 18f), 1f.ToStringPercent());
			Widgets.Label(new Rect(rect.x, plotRect.yMax - 12f, 38f, 18f), MinMultiplier.ToStringPercent());
		}
	}

	private void DrawPermanentPreview(Listing_Standard listing) {
		var rect = listing.GetRect(_PREVIEW_HEIGHT);
		Widgets.DrawMenuSection(rect);
		var inner = rect.Padding(12f);
		var rows = inner.ToFlexbox(FlexDirection.Column, [24, 24, "1fr"], 4f, JustifyContent.SpaceBetween).ToArray();

		using (new TextBlock(GameFont.Medium, TextAnchor.MiddleCenter))
			Widgets.Label(rows[0], TranslateKey("PermanentChart.title"));

		var preview = BuildPermanentPreview();
		DrawPreviewMetric(rows[1], 0, FormatKey("PermanentChart.neutralWork", FormatReps(preview.NeutralReps)));
		DrawPreviewMetric(rows[1], 1, FormatKey("PermanentChart.masteryWork", FormatReps(preview.MasteryReps)));

		DrawPermanentChart(rows[2], preview);
	}

	private PermanentPreview BuildPermanentPreview() {
		float neutralMomentum = WorkMemoryCurve.GetMomentumForMultiplier(
			1f,
			WorkMemoryCurve.MIN_REFERENCE_WORK_AMOUNT,
			MinMultiplier,
			MaxMultiplier
		);
		float momentumCap = WorkMemoryCurve.GetMomentumCap(WorkMemoryCurve.MIN_REFERENCE_WORK_AMOUNT);
		float ceilingMomentum = momentumCap * _permanentMaxFraction;
		float neutralReps = ceilingMomentum > 0f && neutralMomentum < ceilingMomentum
			? RepsForFraction(neutralMomentum / ceilingMomentum)
			: -1f;
		float masteryReps = RepsForFraction(0.9f);
		return new PermanentPreview(neutralReps, masteryReps);
	}

	private float RepsForFraction(float fraction) {
		fraction = Mathf.Clamp(fraction, 0f, 0.999f);
		return _permanentScale * (Mathf.Pow(1f - fraction, -1f / Mathf.Max(_permanentCurvature, 0.01f)) - 1f);
	}

	private void DrawPermanentChart(Rect rect, PermanentPreview preview) {
		Widgets.DrawBoxSolidWithOutline(rect, _chartBackground, _chartBorder);
		var plotRect = rect.Padding(14f, 14f, 26f, 44f);
		DrawChartGrid(plotRect);
		float xMax = Mathf.Max(1f, preview.MasteryReps, preview.NeutralReps * 1.25f);
		var prev = GetPermanentPoint(0f, plotRect, xMax);
		for (var i = 1; i <= _CHART_SEGMENTS; i++) {
			float reps = xMax * i / _CHART_SEGMENTS;
			var current = GetPermanentPoint(reps, plotRect, xMax);
			Widgets.DrawLine(prev, current, _chartLine, 1.6f);
			prev = current;
		}
		if (preview.NeutralReps >= 0f)
			DrawRepsMilestone(plotRect, preview.NeutralReps, xMax);
		DrawRepsMilestone(plotRect, preview.MasteryReps, xMax);
		DrawChartLabels(rect, plotRect);
	}

	private Vector2 GetPermanentPoint(float reps, Rect plotRect, float xMax) {
		float momentum = WorkMemoryCurve.GetPermanentMomentum(
			reps * WorkMemoryCurve.MIN_REFERENCE_WORK_AMOUNT,
			WorkMemoryCurve.MIN_REFERENCE_WORK_AMOUNT,
			_permanentScale,
			_permanentCurvature,
			_permanentMaxFraction
		);
		float multiplier = WorkMemoryCurve.GetMultiplier(momentum, WorkMemoryCurve.MIN_REFERENCE_WORK_AMOUNT, MinMultiplier, MaxMultiplier);
		float x = Mathf.Lerp(plotRect.xMin, plotRect.xMax, reps / xMax);
		float y = Mathf.Lerp(plotRect.yMax, plotRect.yMin, Mathf.InverseLerp(MinMultiplier, MaxMultiplier, multiplier));
		return new Vector2(x, y);
	}
}