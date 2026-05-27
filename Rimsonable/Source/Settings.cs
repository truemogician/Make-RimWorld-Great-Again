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
[FeaturesEnum]
[Translation("Rimsonable.Settings.Features", ImplicitMembers = true)]
public enum Features : ulong {
	None = 0,

	AllowGrenadesThroughShields = 1 << 0,

	SafeRestLocation = 1 << 1,

	[Feature(ModDependencies = [ModIds.CombatExtended])]
	EnhanceArtilleryMarkers = 1 << 2,

	AutoAvoidProximityActivators = 1 << 3,

	[FeatureIgnore]
	WorkMemory = 1 << 4,

	BuildAtCorners = 1 << 5,

	EmergencyJobOverride = 1 << 6
}

[Translation("Rimsonable.Settings")]
public class Settings : FeatureSettings<Features> {
	private const float _ADDITIONAL_SETTINGS_INDENT = 30f;

	private bool _autoTargetMarksOnNonHostile;

	private bool _emergencyJobInterruptOngoingWork;

	private bool _emergencyJobIgnoreAllowedArea;

	public Settings() : base(Helper.Logger) {
		AfterDrawFeatureRow += (_, args) => {
			var conf = args.Config;
			switch (args) {
				case { Feature: Features.EnhanceArtilleryMarkers, Enabled: true }:
					DrawSubSetting(args, conf, nameof(AutoTargetMarksOnNonHostile), ref _autoTargetMarksOnNonHostile); break;
				case { Feature: Features.EmergencyJobOverride, Enabled: true }:
					DrawSubSetting(args, conf, nameof(EmergencyJobInterruptOngoingWork), ref _emergencyJobInterruptOngoingWork);
					DrawSubSetting(args, conf, nameof(EmergencyJobIgnoreAllowedArea), ref _emergencyJobIgnoreAllowedArea);
					break;
			}
		};
	}

	static Settings() {
		AddFeaturePatches(Features.AllowGrenadesThroughShields, typeof(AllowGrenadesThroughShields));
		AddFeaturePatches(Features.SafeRestLocation, typeof(SafeRestLocation));
		AddFeaturePatches(Features.AutoAvoidProximityActivators, typeof(AutoAvoidProximityActivators));
		AddFeaturePatches(Features.BuildAtCorners, typeof(BuildAtCorners));
		AddFeaturePatches(Features.EmergencyJobOverride, typeof(EmergencyJobOverride));
	}

	public static Settings Default { get; internal set; } = null!;

	protected override Harmony Harmony { get; } = new(ThisAssembly.Project.PackageId);

	[Translation]
	public bool AutoTargetMarksOnNonHostile => _autoTargetMarksOnNonHostile;

	[Translation]
	public bool EmergencyJobInterruptOngoingWork => _emergencyJobInterruptOngoingWork;

	[Translation]
	public bool EmergencyJobIgnoreAllowedArea => _emergencyJobIgnoreAllowedArea;

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref _autoTargetMarksOnNonHostile, nameof(AutoTargetMarksOnNonHostile).ToCamelCase());
		Scribe_Values.Look(ref _emergencyJobInterruptOngoingWork, nameof(EmergencyJobInterruptOngoingWork).ToCamelCase());
		Scribe_Values.Look(ref _emergencyJobIgnoreAllowedArea, nameof(EmergencyJobIgnoreAllowedArea).ToCamelCase());
		Notices.ExposeData();
	}

	private static void DrawSubSetting(DrawFeatureRowEventArgs args, SettingsMenuConfig conf, string memberName, ref bool value) {
		var rect = args.NewLine().Padding(0, conf.ResetButtonWidth + conf.Gap, 0, _ADDITIONAL_SETTINGS_INDENT);
		if (Translate(memberName, "description") is { } tip && !tip.NullOrEmpty())
			TooltipHandler.TipRegion(rect, tip);
		Widgets.CheckboxLabeled(rect, Translate(memberName, "label"), ref value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string? Translate(string memberName, string? subField = null) =>
		typeof(Settings).TranslateMember(memberName, subField);
}