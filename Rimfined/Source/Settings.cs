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

	CaptureAsJob = 1 << 1,

	AmbrosiaAutoHarvest = 1 << 2,

	DelayedQuestAcceptance = 1 << 3,

	ConstructionPriority = 1 << 4,

	ShipChunkAutoDeconstruct = 1 << 5
}

[Translation("Rimfined.Settings")]
public class Settings : FeatureSettings<Features> {
	private const float _ADDITIONAL_SETTINGS_INDENT = 30f;

	private const float _SLIDER_LABEL_WIDTH = 100f;

	private const float _SLIDER_WIDTH = 300f;

	private bool _autoNoTargetForPrisonerRelatives;

	private byte _defaultNoTargetMarkTtlHours = 24;

	public Settings() : base(Helper.Logger) {
		AfterDrawFeatureRow += (_, args) => {
			var conf = args.Config;
			if (args is { Feature: Features.NoTarget, Enabled: true }) {
				var rect = args.NewLine().Padding(0, conf.ResetButtonWidth + conf.Gap, 0, _ADDITIONAL_SETTINGS_INDENT);
				if (Translate(nameof(AutoNoTargetForPrisonerRelatives), "description") is { } tip && !tip.NullOrEmpty())
					TooltipHandler.TipRegion(rect, tip);
				Widgets.CheckboxLabeled(
					rect,
					Translate(nameof(AutoNoTargetForPrisonerRelatives), "label"),
					ref _autoNoTargetForPrisonerRelatives
				);

				var rects = args.NewLine()
					.Padding(0, conf.ResetButtonWidth + conf.Gap, 0, _ADDITIONAL_SETTINGS_INDENT)
					.ToFlexbox([Flexbox.Length.Auto, _SLIDER_LABEL_WIDTH, _SLIDER_WIDTH], conf.Gap)
					.ToArray();
				if (Translate(nameof(DefaultNoTargetMarkTtl), "description") is { } tip2 && !tip2.NullOrEmpty())
					TooltipHandler.TipRegion(rects[0], tip2);
				Widgets.Label(rects[0], Translate(nameof(DefaultNoTargetMarkTtl), "label"));
				string? sliderLabel = _defaultNoTargetMarkTtlHours == 0
					? Translate(nameof(DefaultNoTargetMarkTtl), "slider.permanent")
					: Translate(nameof(DefaultNoTargetMarkTtl), "slider.label") is not { } fmt ? null
						: string.Format(fmt, _defaultNoTargetMarkTtlHours);
				using (new TextBlock(TextAnchor.MiddleRight))
					Widgets.Label(rects[1], sliderLabel);
				_defaultNoTargetMarkTtlHours = (byte)Widgets.HorizontalSlider(
					rects[2],
					_defaultNoTargetMarkTtlHours,
					0,
					48,
					true,
					roundTo: 1
				);
			}
		};
	}

	static Settings() {
		AddFeaturePatches(Features.NoTarget, typeof(NoTargetPatches), typeof(NoTargetScopePatches));
		AddFeaturePatches(Features.CaptureAsJob, typeof(CaptureAsJobPatches));
		AddFeaturePatches(Features.AmbrosiaAutoHarvest, typeof(AmbrosiaAutoHarvestPatches));
		AddFeaturePatches(Features.DelayedQuestAcceptance, typeof(DelayedQuestAcceptancePatches));
		AddFeaturePatches(Features.ConstructionPriority, typeof(ConstructionPriorityPatches));
		AddFeaturePatches(Features.ShipChunkAutoDeconstruct, typeof(ShipChunkAutoDeconstructPatches));
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