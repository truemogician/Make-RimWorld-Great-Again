using System;
using System.Linq;
using System.Runtime.CompilerServices;
using CaseExtensions;
using HarmonyLib;
using TrueMogician.RimWorld.RemedialAlgorithms.Patches;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;
using TrueMogician.RimWorld.Utility.Extensions;
using TrueMogician.RimWorld.Utility.GUI;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.RemedialAlgorithms;

[Flags]
[FeaturesEnum]
[Translation("RemedialAlgorithms.Settings", ImplicitMembers = true)]
public enum Optimizations : ulong {
	None = 0,

	TradeSetup = 1 << 0,

	ThingFilterToggle = 1 << 1,

	WildAnimalFoodSearch = 1 << 2
}

[Translation("RemedialAlgorithms.Settings")]
public class Settings : FeatureSettings<Optimizations> {
	private const float _ADDITIONAL_SETTINGS_INDENT = 30f;

	private const float _SLIDER_LABEL_WIDTH = 100f;

	private const float _SLIDER_WIDTH = 300f;

	private int _wildAnimalFoodSearchCacheTtlSeconds = 30;

	private int _wildAnimalFoodSearchStarvingCacheTtlSeconds = 5;

	public Settings() : base(Helper.Logger) {
		AfterDrawFeatureRow += (_, args) => {
			if (args is not { Feature: Optimizations.WildAnimalFoodSearch, Enabled: true })
				return;
			DrawTtlSlider(args, nameof(WildAnimalFoodSearchCacheTtl), ref _wildAnimalFoodSearchCacheTtlSeconds, 5, 300);
			DrawTtlSlider(args, nameof(WildAnimalFoodSearchStarvingCacheTtl), ref _wildAnimalFoodSearchStarvingCacheTtlSeconds, 1, 60);
		};
	}

	static Settings() {
		AddFeaturePatches(Optimizations.TradeSetup, typeof(TradeDealPatches));
		AddFeaturePatches(Optimizations.ThingFilterToggle, typeof(ThingFilterPatches));
		AddFeaturePatches(Optimizations.WildAnimalFoodSearch, typeof(FoodSearchOptimizationPatches));
	}

	public static Settings Default { get; internal set; } = null!;

	protected override Harmony Harmony { get; } = new(ThisAssembly.Project.PackageId);

	[Translation]
	public int WildAnimalFoodSearchCacheTtl => _wildAnimalFoodSearchCacheTtlSeconds * GenTicks.TicksPerRealSecond;

	[Translation]
	public int WildAnimalFoodSearchStarvingCacheTtl => _wildAnimalFoodSearchStarvingCacheTtlSeconds * GenTicks.TicksPerRealSecond;

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref _wildAnimalFoodSearchCacheTtlSeconds, nameof(WildAnimalFoodSearchCacheTtl).ToCamelCase(), 30);
		Scribe_Values.Look(ref _wildAnimalFoodSearchStarvingCacheTtlSeconds, nameof(WildAnimalFoodSearchStarvingCacheTtl).ToCamelCase(), 5);
	}

	private static void DrawTtlSlider(DrawFeatureRowEventArgs args, string memberName, ref int seconds, int min, int max) {
		var conf = args.Config;
		var rects = args.NewLine()
			.Padding(0, conf.ResetButtonWidth + conf.Gap, 0, _ADDITIONAL_SETTINGS_INDENT)
			.ToFlexbox([Flexbox.Length.Auto, _SLIDER_LABEL_WIDTH, _SLIDER_WIDTH], conf.Gap)
			.ToArray();
		if (Translate(memberName, "description") is { } tip && !tip.NullOrEmpty())
			TooltipHandler.TipRegion(rects[0], tip);
		Widgets.Label(rects[0], Translate(memberName, "label"));
		string sliderLabel = Translate(memberName, "slider.label") is { } fmt ? string.Format(fmt, seconds) : seconds + "s";
		using (new TextBlock(TextAnchor.MiddleRight))
			Widgets.Label(rects[1], sliderLabel);
		seconds = WidgetsExtension.HorizontalSlider(rects[2], seconds, min, max, true);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string? Translate(string memberName, string? subField = null) =>
		typeof(Settings).TranslateMember(memberName, subField);
}