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
	TargetMarkEnhancement = 1 << 2,

	AutoAvoidMechDetectors = 1 << 3
}

[Translation("Rimsonable.Settings")]
public class Settings : FeatureSettings<Features> {
	private const float _ADDITIONAL_SETTINGS_INDENT = 30f;

	private bool _autoTargetMarksOnNonHostile;

	public Settings() : base(Helper.Logger) {
		AfterDrawFeatureRow += (_, args) => {
			var conf = args.Config;
			if (args is { Feature: Features.TargetMarkEnhancement, Enabled: true }) {
				var rect = args.NewLine().Padding(0, conf.ResetButtonWidth + conf.Gap, 0, _ADDITIONAL_SETTINGS_INDENT);
				if (Translate(nameof(AutoTargetMarksOnNonHostile), "description") is { } tip && !tip.NullOrEmpty())
					TooltipHandler.TipRegion(rect, tip);
				Widgets.CheckboxLabeled(rect, Translate(nameof(AutoTargetMarksOnNonHostile), "label"), ref _autoTargetMarksOnNonHostile);
			}
		};
	}

	static Settings() {
		AddFeaturePatches(Features.AllowGrenadesThroughShields, typeof(AllowGrenadesThroughShields));
		AddFeaturePatches(Features.SafeRestLocation, typeof(SafeRestLocation));
		AddFeaturePatches(Features.AutoAvoidMechDetectors, typeof(AutoAvoidMechDetectors));
	}

	public static Settings Default { get; internal set; } = null!;

	protected override Harmony Harmony { get; } = new(ThisAssembly.Project.PackageId);

	[Translation]
	public bool AutoTargetMarksOnNonHostile => _autoTargetMarksOnNonHostile;

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref _autoTargetMarksOnNonHostile, nameof(AutoTargetMarksOnNonHostile).ToCamelCase());
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string? Translate(string memberName, string? subField = null)
		=> typeof(Settings).TranslateMember(memberName, subField);
}