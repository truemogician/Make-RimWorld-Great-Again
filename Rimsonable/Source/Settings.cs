using System;
using System.Runtime.CompilerServices;
using CaseExtensions;
using HarmonyLib;
using TrueMogician.RimWorld.Rimsonable.Patches;
using TrueMogician.RimWorld.Rimsonable.Static;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;
using TrueMogician.RimWorld.Utility.Extensions;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable;

[Flags]
[FeaturesEnum(true)]
[Translation("Rimsonable.Settings.Features", ImplicitMembers = true)]
public enum Features : ulong {
	None = 0,

	AllowGrenadesThroughShields = 1 << 0,

	SafeRestLocation = 1 << 1,

	[Feature(ModDependencies = [ModIds.CombatExtended])]
	EnhanceArtilleryMarkers = 1 << 2,

	AutoAvoidProximityActivators = 1 << 3,

	WorkMemory = 1 << 4,

	BuildAtCorners = 1 << 5
}

[Translation("Rimsonable.Settings")]
public class Settings : FeatureSettings<Features> {
	private const float _ADDITIONAL_SETTINGS_INDENT = 30f;

	private bool _autoTargetMarksOnNonHostile;

	private bool _workMemoryNonQualityRecipes;

	public Settings() : base(Helper.Logger) {
		AfterDrawFeatureRow += (_, args) => {
			var conf = args.Config;
			if (args is { Feature: Features.EnhanceArtilleryMarkers, Enabled: true })
				DrawSubSetting(args, conf, nameof(AutoTargetMarksOnNonHostile), ref _autoTargetMarksOnNonHostile);
			if (args is { Feature: Features.WorkMemory, Enabled: true })
				DrawSubSetting(args, conf, nameof(WorkMemoryNonQualityRecipes), ref _workMemoryNonQualityRecipes);
		};
	}

	static Settings() {
		AddFeaturePatches(Features.AllowGrenadesThroughShields, typeof(AllowGrenadesThroughShields));
		AddFeaturePatches(Features.SafeRestLocation, typeof(SafeRestLocation));
		AddFeaturePatches(Features.AutoAvoidProximityActivators, typeof(AutoAvoidProximityActivators));
		AddFeaturePatches(Features.WorkMemory, typeof(WorkMemory));
		AddFeaturePatches(Features.BuildAtCorners, typeof(BuildAtCorners));
	}

	public static Settings Default { get; internal set; } = null!;

	protected override Harmony Harmony { get; } = new(ThisAssembly.Project.PackageId);

	[Translation]
	public bool AutoTargetMarksOnNonHostile => _autoTargetMarksOnNonHostile;

	[Translation]
	public bool WorkMemoryNonQualityRecipes => _workMemoryNonQualityRecipes;

	private static void DrawSubSetting(DrawFeatureRowEventArgs args, SettingsMenuConfig conf, string memberName, ref bool value) {
		var rect = args.NewLine().Padding(0, conf.ResetButtonWidth + conf.Gap, 0, _ADDITIONAL_SETTINGS_INDENT);
		if (Translate(memberName, "description") is { } tip && !tip.NullOrEmpty())
			TooltipHandler.TipRegion(rect, tip);
		Widgets.CheckboxLabeled(rect, Translate(memberName, "label"), ref value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string? Translate(string memberName, string? subField = null)
		=> typeof(Settings).TranslateMember(memberName, subField);

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref _autoTargetMarksOnNonHostile, nameof(AutoTargetMarksOnNonHostile).ToCamelCase());
		Scribe_Values.Look(ref _workMemoryNonQualityRecipes, nameof(WorkMemoryNonQualityRecipes).ToCamelCase());
	}
}
