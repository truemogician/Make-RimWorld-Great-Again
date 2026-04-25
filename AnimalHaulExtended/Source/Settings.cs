using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.AnimalHaulExtended;

public enum HaulJobPreset : byte {
	RequireHaulCapability,
	HaulingWorkType,
	Custom
}

public sealed class Settings : ModSettings {
	private const string _TRANSLATION_PREFIX = "AnimalHaulExtended.Settings";

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
		_harmony.CreateClassProcessor(typeof(AnimalHaulExtension)).Patch();
		_patched = true;
	}

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref _preset, "preset", HaulJobPreset.RequireHaulCapability);
		Scribe_Collections.Look(ref _disabledCustomWorkGivers, "disabledCustomWorkGivers", LookMode.Value);
		_disabledCustomWorkGivers ??= [];
		InvalidateCaches();
	}

	public void DrawContents(Rect inRect) {
		Rect presetRect = new(inRect.x, inRect.y, inRect.width, 190f);
		Rect customRect = new(inRect.x, presetRect.yMax + 12f, inRect.width, Mathf.Max(0f, inRect.height - presetRect.height - 12f));

		DrawPresetSection(presetRect);
		if (customRect.height > 0f)
			DrawCustomSection(customRect);
	}

	internal bool IsWorkGiverEnabled(WorkGiverDef def) => IsWorkGiverEnabled(def, _preset);

	private IReadOnlyList<WorkGiver> BuildEnabledWorkGivers()
		=> GetEnabledWorkGiverDefs()
			.Select(def => def.Worker)
			.Where(worker => worker != null)
			.ToArray();

	private void DrawCustomSection(Rect rect) {
		Widgets.DrawMenuSection(rect);
		var innerRect = rect.ContractedBy(8f);
		Rect headerRect = new(innerRect.x, innerRect.y, innerRect.width, 30f);
		float buttonWidth = Mathf.Min(innerRect.width * 0.45f, Mathf.Max(190f, Text.CalcSize(Translate("CustomList.ResetLabel")).x + 28f));
		Rect buttonRect = new(headerRect.xMax - buttonWidth, headerRect.y, buttonWidth, headerRect.height);
		Rect labelRect = new(headerRect.x, headerRect.y, headerRect.width - buttonRect.width - 8f, headerRect.height);

		Widgets.Label(labelRect, Translate("CustomList.Label"));
		TooltipHandler.TipRegion(labelRect, Translate("CustomList.Description"));
		if (Widgets.ButtonText(buttonRect, Translate("CustomList.ResetLabel")))
			ResetCustomWorkGivers();

		Rect outRect = new(innerRect.x, headerRect.yMax + 6f, innerRect.width, innerRect.height - headerRect.height - 6f);
		const float rowHeight = 28f;
		Rect viewRect = new(0f, 0f, outRect.width - 16f, HaulWorkGiverCatalog.AllHaulingWorkGivers.Count * rowHeight);

		Widgets.BeginScrollView(outRect, ref _customScrollPosition, viewRect);
		var y = 0f;
		foreach (var def in HaulWorkGiverCatalog.AllHaulingWorkGivers) {
			Rect rowRect = new(0f, y, viewRect.width, 24f);
			bool enabled = IsWorkGiverEnabled(def);
			var label = def.LabelCap;
			TooltipHandler.TipRegion(rowRect, def.LabelCap);
			Widgets.CheckboxLabeled(rowRect, label, ref enabled);
			if (enabled != IsWorkGiverEnabled(def))
				ToggleWorkGiver(def, enabled);
			y += rowHeight;
		}
		Widgets.EndScrollView();
	}

	private void DrawPresetSection(Rect rect) {
		Widgets.DrawMenuSection(rect);
		var listing = new Listing_Standard();
		listing.Begin(rect.ContractedBy(8f));
		listing.Label(Translate("Preset.Label"));
		if (TranslateOrNull("Preset.Description") is { } description)
			listing.Label(description);
		listing.Gap(6f);

		DrawPresetOption(ref listing, HaulJobPreset.RequireHaulCapability, "PresetMode.RequireHaulCapability");
		DrawPresetOption(ref listing, HaulJobPreset.HaulingWorkType, "PresetMode.HaulingWorkType");
		DrawPresetOption(ref listing, HaulJobPreset.Custom, "PresetMode.Custom");

		listing.End();
	}

	private void DrawPresetOption(ref Listing_Standard listing, HaulJobPreset preset, string keySuffix) {
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

		HaulJobPreset previousPreset = _preset;
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

	private static string Translate(string suffix)
		=> $"{_TRANSLATION_PREFIX}.{suffix}".Translate();

	private static string? TranslateOrNull(string suffix) {
		var key = $"{_TRANSLATION_PREFIX}.{suffix}";
		return key.TryTranslate(out var translated) ? translated.Resolve() : null;
	}
}