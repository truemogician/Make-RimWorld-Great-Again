using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Utility.Extensions;
using TrueMogician.RimWorld.Utility.GUI;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;

namespace TrueMogician.RimWorld.AnimalHaulExtended;

public enum HaulJobPreset : byte {
	RequireHaulCapability,
	HaulingWorkType,
	Custom
}

public sealed class Settings : ModSettings {
	private const string _TRANSLATION_PREFIX = "AnimalHaulExtended.Settings";

	private const float _PRESET_SECTION_HEIGHT = 190f;

	private const float _SECTION_GAP = 12f;

	private const float _SECTION_PADDING = 8f;

	private const float _CUSTOM_HEADER_HEIGHT = 30f;

	private const float _CUSTOM_HEADER_GAP = 6f;

	private const float _CUSTOM_ROW_HEIGHT = 28f;

	private readonly Harmony _harmony = new(ThisAssembly.Project.PackageId);

	private List<string> _disabledCustomWorkGivers = [];

	private IReadOnlyList<WorkGiver>? _enabledWorkGivers;

	private bool _patched;

	private HaulJobPreset _preset = HaulJobPreset.RequireHaulCapability;

	private Vector2 _customScrollPosition;

	public static Settings Default { get; internal set; } = null!;

	internal IReadOnlyList<WorkGiver> EnabledWorkGivers => _enabledWorkGivers ??= BuildEnabledWorkGivers();

	public void Apply() {
		if (_patched)
			return;
		_harmony.CreateClassProcessor(typeof(MainPatch)).Patch();
		_patched = true;
	}

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref _preset, "preset", HaulJobPreset.RequireHaulCapability);
		Scribe_Collections.Look(ref _disabledCustomWorkGivers, "disabledCustomWorkGivers", LookMode.Value);
		_disabledCustomWorkGivers ??= [];
		if (Scribe.mode == LoadSaveMode.LoadingVars)
			InvalidateCaches();
	}

	public void DrawContents(Rect inRect) {
		var rects = inRect
			.ToFlexbox(FlexDirection.Column, [_PRESET_SECTION_HEIGHT, Flexbox.Length.Auto], _SECTION_GAP)
			.ToArray();
		DrawPresetSection(rects[0]);
		if (rects[1].height > 0f)
			DrawCustomSection(rects[1]);
	}

	internal bool IsWorkGiverEnabled(WorkGiverDef def) => IsWorkGiverEnabled(def, _preset);

	private static string Translate(string suffix)
		=> $"{_TRANSLATION_PREFIX}.{suffix}".Translate();

	private static string? TranslateOrNull(string suffix) {
		var key = $"{_TRANSLATION_PREFIX}.{suffix}";
		return key.TryTranslate(out var translated) ? translated.Resolve() : null;
	}

	private IReadOnlyList<WorkGiver> BuildEnabledWorkGivers()
		=> GetEnabledWorkGiverDefs()
			.Select(def => def.Worker)
			.Where(worker => worker != null)
			.ToArray();

	private void DrawCustomSection(Rect rect) {
		Widgets.DrawMenuSection(rect);
		var inner = rect.Padding(_SECTION_PADDING);
		var rows = inner.ToFlexbox(FlexDirection.Column, [_CUSTOM_HEADER_HEIGHT, Flexbox.Length.Auto], _CUSTOM_HEADER_GAP).ToArray();
		DrawCustomHeader(rows[0]);
		DrawCustomList(rows[1]);
	}

	private void DrawCustomHeader(Rect headerRect) {
		string resetLabel = Translate("CustomList.ResetLabel");
		float buttonWidth = Mathf.Min(headerRect.width * 0.45f, Mathf.Max(190f, Text.CalcSize(resetLabel).x + 28f));
		var cells = headerRect.ToFlexbox([Flexbox.Length.Auto, buttonWidth], _SECTION_PADDING).ToArray();

		Widgets.Label(cells[0], Translate("CustomList.Label"));
		TooltipHandler.TipRegion(cells[0], Translate("CustomList.Description"));
		if (Widgets.ButtonText(cells[1], resetLabel))
			ResetCustomWorkGivers();
	}

	private void DrawCustomList(Rect outRect) {
		Rect viewRect = new(0f, 0f, outRect.width - 16f, HaulWorkGiverCatalog.AllHaulingWorkGivers.Count * _CUSTOM_ROW_HEIGHT);

		Widgets.BeginScrollView(outRect, ref _customScrollPosition, viewRect);
		var y = 0f;
		foreach (var def in HaulWorkGiverCatalog.AllHaulingWorkGivers) {
			Rect rowRect = new(0f, y, viewRect.width, _CUSTOM_ROW_HEIGHT - 4f);
			bool enabled = IsWorkGiverEnabled(def);
			TooltipHandler.TipRegion(rowRect, def.LabelCap);
			Widgets.CheckboxLabeled(rowRect, def.LabelCap, ref enabled);
			if (enabled != IsWorkGiverEnabled(def))
				ToggleWorkGiver(def, enabled);
			y += _CUSTOM_ROW_HEIGHT;
		}
		Widgets.EndScrollView();
	}

	private void DrawPresetSection(Rect rect) {
		Widgets.DrawMenuSection(rect);
		var listing = new Listing_Standard();
		listing.Begin(rect.Padding(_SECTION_PADDING));
		listing.Label(Translate("Preset.Label"));
		if (TranslateOrNull("Preset.Description") is { } description)
			listing.Label(description);
		listing.Gap(_CUSTOM_HEADER_GAP);

		DrawPresetOption(listing, HaulJobPreset.RequireHaulCapability, "PresetMode.RequireHaulCapability");
		DrawPresetOption(listing, HaulJobPreset.HaulingWorkType, "PresetMode.HaulingWorkType");
		DrawPresetOption(listing, HaulJobPreset.Custom, "PresetMode.Custom");

		listing.End();
	}

	private void DrawPresetOption(Listing_Standard listing, HaulJobPreset preset, string keySuffix) {
		if (listing.RadioButton(Translate($"{keySuffix}.Label"), _preset == preset, tooltip: TranslateOrNull($"{keySuffix}.Description")))
			SetPreset(preset);
	}

	private IReadOnlyList<WorkGiverDef> GetEnabledWorkGiverDefs() => _preset switch {
		HaulJobPreset.HaulingWorkType => HaulWorkGiverCatalog.AllHaulingWorkGivers,
		HaulJobPreset.Custom          => HaulWorkGiverCatalog.AllHaulingWorkGivers.Where(IsCustomEnabled).ToArray(),
		_                             => HaulWorkGiverCatalog.HaulCapabilityWorkGivers
	};

	private void InvalidateCaches() => _enabledWorkGivers = null;

	private bool IsCustomEnabled(WorkGiverDef def)
		=> !_disabledCustomWorkGivers.Contains(def.defName);

	private bool IsWorkGiverEnabled(WorkGiverDef def, HaulJobPreset preset) => preset switch {
		HaulJobPreset.HaulingWorkType => HaulWorkGiverCatalog.AllHaulingWorkGivers.Contains(def),
		HaulJobPreset.Custom          => IsCustomEnabled(def),
		_                             => HaulWorkGiverCatalog.HaulCapabilityWorkGivers.Contains(def)
	};

	private void ResetCustomWorkGivers() {
		_preset = HaulJobPreset.Custom;
		_disabledCustomWorkGivers.Clear();
		InvalidateCaches();
	}

	private void SetCustomEnabled(WorkGiverDef def, bool enabled) {
		if (enabled)
			_disabledCustomWorkGivers.Remove(def.defName);
		else if (!_disabledCustomWorkGivers.Contains(def.defName))
			_disabledCustomWorkGivers.Add(def.defName);
		_disabledCustomWorkGivers.Sort(StringComparer.Ordinal);
		InvalidateCaches();
	}

	private void ToggleWorkGiver(WorkGiverDef def, bool enabled) {
		PromoteCurrentSelectionToCustom();
		SetCustomEnabled(def, enabled);
	}

	private void PromoteCurrentSelectionToCustom() {
		if (_preset == HaulJobPreset.Custom)
			return;

		var previousPreset = _preset;
		_disabledCustomWorkGivers = HaulWorkGiverCatalog.AllHaulingWorkGivers
			.Where(def => !IsWorkGiverEnabled(def, previousPreset))
			.Select(def => def.defName)
			.OrderBy(defName => defName, StringComparer.Ordinal)
			.ToList();
		_preset = HaulJobPreset.Custom;
		InvalidateCaches();
	}

	private void SetPreset(HaulJobPreset preset) {
		if (_preset == preset)
			return;
		_preset = preset;
		InvalidateCaches();
	}
}