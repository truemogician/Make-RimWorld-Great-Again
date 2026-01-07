using System;
using System.Linq;
using System.Runtime.CompilerServices;
using CaseExtensions;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Rimfined.Patches;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;
using TrueMogician.RimWorld.Utility.Extensions;
using TrueMogician.RimWorld.Utility.GUI;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Rimfined;

[Flags]
[FeaturesEnum(true)]
[Translation("Rimfined.Settings.Features", ImplicitMembers = true)]
public enum Features : ulong {
	None = 0,

	NoTarget = 1 << 0,

	CaptureAsJob = 1 << 1
}

[Translation("Rimfined.Settings")]
public class Settings : FeatureSettings<Features> {
	private const float _ADDITIONAL_SETTINGS_INDENT = 30f;

	private const float _SLIDER_WIDTH = 240f;

	private bool _autoNoTargetForPrisonerRelatives;

	private byte _defaultNoTargetMarkTtlHours = 24;

	public Settings() : base(Helper.Logger) {
		AfterDrawFeatureRow += (_, args) => {
			if (args.Feature == Features.NoTarget && args.Enabled) {
				var rects = args.Listing.GetRect(Mathf.Max(Text.LineHeight, 24f))
					.ToFlexbox([_ADDITIONAL_SETTINGS_INDENT, Flexbox.Length.Auto, args.Config.ResetButtonWidth], args.Config.RowGap)
					.ToArray();
				if (Translate(nameof(AutoNoTargetForPrisonerRelatives), "description") is { } tip && !tip.NullOrEmpty())
					TooltipHandler.TipRegion(rects[1], tip);
				Widgets.CheckboxLabeled(
					rects[1],
					Translate(nameof(AutoNoTargetForPrisonerRelatives), "label"),
					ref _autoNoTargetForPrisonerRelatives
				);

				rects = args.Listing.GetRect(Mathf.Max(Text.LineHeight, 24f))
					.ToFlexbox([_ADDITIONAL_SETTINGS_INDENT, Flexbox.Length.Auto, _SLIDER_WIDTH, args.Config.ResetButtonWidth], args.Config.RowGap)
					.ToArray();
				if (Translate(nameof(DefaultNoTargetMarkTtl), "description") is { } tip2 && !tip2.NullOrEmpty())
					TooltipHandler.TipRegion(rects[1], tip2);
				Widgets.Label(rects[1], Translate(nameof(DefaultNoTargetMarkTtl), "label"));
				_defaultNoTargetMarkTtlHours = (byte)Widgets.HorizontalSlider(rects[2], _defaultNoTargetMarkTtlHours, 0, 48, roundTo: 1);
			}
		};
	}

	static Settings() {
		AddFeaturePatches(Features.NoTarget, typeof(NoTargetPatches), typeof(NoTargetScopePatches));
		AddFeaturePatches(Features.CaptureAsJob, typeof(CaptureAsJobPatches));
	}

	public static Settings Default { get; internal set; } = null!;

	protected override Harmony Harmony { get; } = new(ThisAssembly.Project.PackageId);

	[Translation]
	public bool AutoNoTargetForPrisonerRelatives => _autoNoTargetForPrisonerRelatives;

	[Translation]
	public int DefaultNoTargetMarkTtl => _defaultNoTargetMarkTtlHours == 0 ? -1 : _defaultNoTargetMarkTtlHours * GenDate.TicksPerHour;

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref _autoNoTargetForPrisonerRelatives, nameof(AutoNoTargetForPrisonerRelatives).ToCamelCase());
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string? Translate(string memberName, string? subField = null)
		=> typeof(Settings).TranslateMember(memberName, subField);
}