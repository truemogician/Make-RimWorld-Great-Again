using System;
using System.Linq;
using System.Runtime.CompilerServices;
using CaseExtensions;
using HarmonyLib;
using TrueMogician.RimWorld.Rimsonable.Patches;
using TrueMogician.RimWorld.Rimsonable.Static;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;
using TrueMogician.RimWorld.Utility.Extensions;
using TrueMogician.RimWorld.Utility.GUI;
using UnityEngine;
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
	TargetMarkEnhancement = 1 << 2
}

[Translation("Rimsonable.Settings")]
public class Settings : FeatureSettings<Features> {
	private bool _autoTargetMarksOnNonHostile;

	public Settings() : base(Helper.Logger) {
		AfterDrawFeatureRow += (_, args) => {
			if (args.Feature == Features.TargetMarkEnhancement) {
				var rects = args.Listing.GetRect(Mathf.Max(Text.LineHeight, 24f))
					.ToFlexbox([30, Flexbox.Length.Auto, args.Config.ResetButtonWidth], args.Config.RowGap)
					.ToArray();
				if (Translate(nameof(AutoTargetMarksOnNonHostile), "description") is { } tip && !tip.NullOrEmpty())
					TooltipHandler.TipRegion(rects[1], tip);
				Widgets.CheckboxLabeled(rects[1], Translate(nameof(AutoTargetMarksOnNonHostile), "label"), ref _autoTargetMarksOnNonHostile);
			}
		};
	}

	static Settings() {
		AddFeaturePatches(Features.AllowGrenadesThroughShields, typeof(AllowGrenadesThroughShields));
		AddFeaturePatches(Features.SafeRestLocation, typeof(SafeRestLocation));
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