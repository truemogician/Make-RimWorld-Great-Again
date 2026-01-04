using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CaseExtensions;
using HarmonyLib;
using JetBrains.Annotations;
using TrueMogician.RimWorld.Utility.Attributes;
using TrueMogician.RimWorld.Utility.GUI;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Utility;

[PublicAPI]
public abstract class FeatureSettings<T>(Logger? logger = null) : ModSettings
	where T : struct, Enum {
	protected static readonly Dictionary<T, HashSet<Type>> FeaturePatches = [];

	protected static readonly ulong All = Enum.GetValues(typeof(T)).Cast<T>().Aggregate(0UL, (a, v) => a | ToUlong(v));

	protected static readonly string? TranslationKeyPrefix = typeof(T).GetTranslationKey();

	protected ISet<Type> AppliedPatches = new HashSet<Type>();

	protected readonly Logger? Logger = logger;

	private ulong _specifiedFeatures;

	private ulong _specifiedMask;

	protected delegate void DrawFeatureRowEventHandler(object sender, DrawFeatureRowEventArgs args);

	protected event DrawFeatureRowEventHandler? AfterDrawFeatureRow;

	protected event DrawFeatureRowEventHandler? BeforeDrawFeatureRow;

	protected record DrawFeatureRowEventArgs(T Feature, Listing_Standard Listing, SettingsMenuConfig Config);

	public record SettingsMenuConfig(
		string ResetButtonText = "Reset",
		float ResetButtonWidth = 80f,
		float RowGap = 10f
	);

	public virtual T DefaultFeatures { get; } = GetDefaultFeatures();

	protected string SpecifiedMaskLabel { get; init; } = "specifiedMask";

	protected string SpecifiedFeaturesLabel { get; init; } = "specifiedFeatures";

	public T Features => FromUlong(FeaturesUlong);

	protected static IReadOnlyList<string> ModIds
		=> field ??= ModsConfig.ActiveModsInLoadOrder.Select(m => m.PackageId).ToArray();

	protected ulong FeaturesUlong => GetEffectiveFeatures(ToUlong(DefaultFeatures), _specifiedFeatures, _specifiedMask);

	protected abstract Harmony Harmony { get; }

	public bool this[T feature] {
		get => this[ToUlong(feature)];
		set => this[ToUlong(feature)] = value;
	}

	protected bool this[ulong feature] {
		get => (FeaturesUlong & feature) == feature;
		set {
			_specifiedMask |= feature;
			if (value)
				_specifiedFeatures |= feature;
			else
				_specifiedFeatures &= ~feature;
		}
	}

	public static void AddFeaturePatches(T feature, params Type[] patchTypes) {
		if (!FeaturePatches.TryGetValue(feature, out var list))
			FeaturePatches[feature] = list = [];
		list.AddRange(patchTypes);
	}

	public static T GetDefaultFeatures() {
		bool defaultEnabled = typeof(T).GetCustomAttribute<FeaturesEnumAttribute>()?.DefaultEnabled ?? true;
		ulong result = defaultEnabled ? All : 0;
		foreach (var (feature, attributes, _) in GetFeatureAttributes()) {
			bool? enabled = attributes.OfType<FeatureAttribute>().FirstOrDefault()?.DefaultEnabled;
			if (enabled.HasValue && enabled.Value != defaultEnabled) {
				if (enabled.Value)
					result |= ToUlong(feature);
				else
					result &= ~ToUlong(feature);
			}
		}
		return FromUlong(result);
	}

	public static IEnumerable<(T Feature, TaggedString Label, TaggedString? Description)> GetSettingsMenuEntries() {
		var modIds = ModIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach ((var feature, var attributes, string? translationKey) in GetFeatureAttributes()) {
			if (attributes.Length == 0) {
				if (!feature.IsSingleBitFlag)
					continue;
			}
			else if (attributes.OfType<FeatureIgnoreAttribute>().Any())
				continue;
			var attr = attributes.OfType<FeatureAttribute>().FirstOrDefault();
			if (attr?.ModDependencies is { Length: > 0 } dependencies && !dependencies.All(modIds.Contains))
				continue;
			if (attr?.ModIncompatibilities is { Length: > 0 } incompatibilities && incompatibilities.Any(modIds.Contains))
				continue;
			TaggedString label;
			TaggedString? description;
			if (translationKey is null) {
				label = attr?.Label ?? feature.ToString().ToTrainCase().Replace('-', ' ');
				description = attr?.Description;
			}
			else {
				label = $"{translationKey}.label".TryTranslate(out var l) ? l : attr?.Label ?? feature.ToString().ToTrainCase().Replace('-', ' ');
				description = $"{translationKey}.description".TryTranslate(out var d) ? d : attr?.Description;
			}
			yield return (feature, label, description);
		}
	}

	public void ResetAllFeatures() {
		_specifiedMask = 0;
		_specifiedFeatures = 0;
	}

	public void ResetFeature(T feature) {
		ulong value = ToUlong(feature);
		_specifiedMask &= ~value;
		_specifiedFeatures &= ~value;
	}

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref _specifiedMask, SpecifiedMaskLabel);
		Scribe_Values.Look(ref _specifiedFeatures, SpecifiedFeaturesLabel);
	}

	public void Apply() {
		var newPatches = GetEnabledPatches();
		var patchesToRemove = AppliedPatches.Except(newPatches).ToList();
		var patchesToAdd = newPatches.Except(AppliedPatches).ToList();
		if (patchesToRemove.Count > 0) {
			foreach (var patch in patchesToRemove)
				Harmony.CreateClassProcessor(patch).Unpatch();
			Logger?.Message($"Removed {patchesToRemove.Count} patches");
		}
		if (patchesToAdd.Count > 0) {
			foreach (var patch in patchesToAdd)
				Harmony.CreateClassProcessor(patch).Patch();
			Logger?.Message($"Applied {patchesToAdd.Count} patches");
		}
		AppliedPatches = newPatches;
	}

	public virtual void DrawContents(Listing_Standard listing, SettingsMenuConfig? config = null) {
		config ??= new SettingsMenuConfig();
		ulong newMask = _specifiedMask;
		ulong newFeatures = _specifiedFeatures;
		ulong curFeatures = FeaturesUlong;

		foreach (var (feature, label, description) in GetSettingsMenuEntries()) {
			ulong featureMask = ToUlong(feature);

			// Compute from locals so the UI reflects edits made earlier in this draw pass.
			bool specified = (newMask & featureMask) != 0;
			ulong effectiveFeatures = (ToUlong(DefaultFeatures) & ~newMask) | (newFeatures & newMask);
			bool enabled = (effectiveFeatures & featureMask) == featureMask;

			var eventArgs = new DrawFeatureRowEventArgs(feature, listing, config);
			BeforeDrawFeatureRow?.Invoke(this, eventArgs);
			var rects = listing.GetRect(Mathf.Max(Text.LineHeight, 24f))
				.ToFlexbox([Flexbox.Length.Auto, config.ResetButtonWidth], config.RowGap)
				.ToArray();
			if (description is { } tip && !tip.NullOrEmpty())
				TooltipHandler.TipRegion(rects[0], tip);
			bool newEnabled = enabled;
			Widgets.CheckboxLabeled(rects[0], label, ref newEnabled);
			if (newEnabled != enabled) {
				newMask |= featureMask;
				if (newEnabled)
					newFeatures |= featureMask;
				else
					newFeatures &= ~featureMask;
				specified = true; // keep button state consistent within the same row/frame
			}
			using (Scoped.GUI(specified)) {
				if (Widgets.ButtonText(rects[1], config.ResetButtonText)) {
					newMask &= ~featureMask;
					newFeatures &= ~featureMask;
				}
			}
			AfterDrawFeatureRow?.Invoke(this, eventArgs);
		}

		if (newMask != _specifiedMask || newFeatures != _specifiedFeatures) {
			_specifiedMask = newMask;
			_specifiedFeatures = newFeatures;
			if (FeaturesUlong != curFeatures)
				Apply();
		}
	}

	public void DrawContents(Rect inRect, SettingsMenuConfig? config = null) {
		var listing = new Listing_Standard();
		listing.Begin(inRect);
		DrawContents(listing, config);
		listing.End();
	}

	protected static ulong ToUlong(T value) => (ulong)Convert.ChangeType(value, typeof(ulong));

	protected static T FromUlong(ulong value) => (T)Enum.ToObject(typeof(T), value);

	protected static ulong GetEffectiveFeatures(ulong @default, ulong specified, ulong mask)
		=> (@default & ~mask) | (specified & mask);

	protected static IEnumerable<(T, FeatureAttributeBase[], string?)> GetFeatureAttributes() {
		var values = Enum.GetValues(typeof(T)).Cast<T>();
		foreach (var value in values) {
			var member = typeof(T).GetMember(value.ToString()).First();
			var attributes = member.GetCustomAttributes(typeof(FeatureAttributeBase), false).Cast<FeatureAttributeBase>().ToArray();
			yield return (value, attributes, member.GetTranslationKey());
		}
	}

	protected static ISet<Type> GetAllPatches() {
		var collection = new HashSet<Type>();
		foreach (var types in FeaturePatches.Values)
			collection.UnionWith(types);
		return collection;
	}

	protected ISet<Type> GetEnabledPatches() {
		var collection = new HashSet<Type>();
		foreach (var (feature, types) in FeaturePatches) {
			if (this[feature])
				collection.UnionWith(types);
		}
		return collection;
	}
}