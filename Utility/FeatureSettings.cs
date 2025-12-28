using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

	protected static readonly ulong All = Enum.GetValues(typeof(T)).Cast<ulong>().Aggregate(0UL, (a, b) => a | b);

	protected static readonly string? TranslationKeyPrefix = typeof(T).GetCustomAttribute<FeaturesEnumAttribute>()?.TranslationKey;

	protected ISet<Type> AppliedPatches = new HashSet<Type>();

	protected readonly Logger? Logger = logger;

	private ulong _specifiedFeatures;

	private ulong _specifiedMask;

	public record SettingsMenuConfig {
		public string ResetButtonText { get; init; } = "Reset";

		public float ResetButtonWidth { get; init; } = 80f;

		public float RowGap { get; init; } = 4f;
	}

	public virtual T DefaultFeatures { get; } = GetDefaultFeatures();

	protected string SpecifiedMaskLabel { get; init; } = "specifiedMask";

	protected string SpecifiedFeaturesLabel { get; init; } = "specifiedFeatures";

	public T Features => FromUlong(FeaturesUlong);

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
		foreach (var (feature, attributes) in GetFeatureAttributes()) {
			var enabled = attributes.OfType<FeatureAttribute>().FirstOrDefault()?.DefaultEnabled;
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
		foreach (var (feature, attributes) in GetFeatureAttributes()) {
			if (attributes.Length == 0) {
				if (!feature.IsSingleBitFlag)
					continue;
			}
			else if (attributes.OfType<FeatureIgnoreAttribute>().Any())
				continue;
			var attr = attributes.OfType<FeatureAttribute>().FirstOrDefault();
			var translationKey = attr?.TranslationKey ?? (TranslationKeyPrefix is { } prefix ? $"{prefix}.{feature}" : null);
			TaggedString label;
			TaggedString? description;
			if (translationKey is null) {
				label = attr?.Label ?? feature.ToString();
				description = attr?.Description;
			}
			else {
				label = $"{translationKey}.label".TryTranslate(out var l) ? l : (attr?.Label ?? feature.ToString());
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
		var value = ToUlong(feature);
		_specifiedMask &= ~value;
		_specifiedFeatures &= ~value;
	}

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref _specifiedMask, SpecifiedMaskLabel);
		Scribe_Values.Look(ref _specifiedFeatures, SpecifiedFeaturesLabel);
	}

	public void Apply() {
		if (AppliedPatches.Count > 0) {
			Harmony.UnpatchAll(Harmony.Id);
			Logger?.Message($"Removed {AppliedPatches.Count} patches");
		}
		AppliedPatches = GetPatchTypes();
		if (AppliedPatches.Count > 0) {
			foreach (var patchType in AppliedPatches)
				Harmony.PatchAll(patchType.Assembly);
			Logger?.Message($"Applied {AppliedPatches.Count} patches");
		}
	}

	public virtual void DrawContents(Listing_Standard listing, SettingsMenuConfig? config = null) {
		config ??= new SettingsMenuConfig();
		var newMask = _specifiedMask;
		var newFeatures = _specifiedFeatures;
		var curFeatures = FeaturesUlong;

		foreach (var (feature, label, description) in GetSettingsMenuEntries()) {
			var featureMask = ToUlong(feature);

			// Compute from locals so the UI reflects edits made earlier in this draw pass.
			bool specified = (newMask & featureMask) != 0;
			ulong effectiveFeatures = (ToUlong(DefaultFeatures) & ~newMask) | (newFeatures & newMask);
			bool enabled = (effectiveFeatures & featureMask) == featureMask;

			var rowRect = listing.GetRect(Mathf.Max(Text.LineHeight, 24f));
			var buttonRect = new Rect(rowRect.xMax - config.ResetButtonWidth, rowRect.y, config.ResetButtonWidth, rowRect.height);
			var checkRect = new Rect(rowRect.x, rowRect.y, rowRect.width - config.ResetButtonWidth - config.RowGap, rowRect.height);

			if (description is { } tip && !tip.NullOrEmpty())
				TooltipHandler.TipRegion(checkRect, tip);

			bool newEnabled = enabled;
			Widgets.CheckboxLabeled(checkRect, label, ref newEnabled, placeCheckboxNearText: true);

			if (newEnabled != enabled) {
				newMask |= featureMask;
				if (newEnabled)
					newFeatures |= featureMask;
				else
					newFeatures &= ~featureMask;
				specified = true; // keep button state consistent within the same row/frame
			}

			using (Scoped.GUI(specified)) {
				if (Widgets.ButtonText(buttonRect, config.ResetButtonText)) {
					newMask &= ~featureMask;
					newFeatures &= ~featureMask;
				}
			}
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

	protected static IEnumerable<(T, FeatureAttributeBase[])> GetFeatureAttributes() {
		var values = Enum.GetValues(typeof(T)).Cast<T>();
		foreach (var value in values) {
			var member = typeof(T).GetMember(value.ToString()).First();
			var attributes = member.GetCustomAttributes(typeof(FeatureAttributeBase), false).Cast<FeatureAttributeBase>().ToArray();
			yield return (value, attributes);
		}
	}

	protected ISet<Type> GetPatchTypes() {
		var collection = new HashSet<Type>();
		foreach (var (feature, types) in FeaturePatches) {
			if (this[feature])
				collection.UnionWith(types);
		}
		return collection;
	}
}