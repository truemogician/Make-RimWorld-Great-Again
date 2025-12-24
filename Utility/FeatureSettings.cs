using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Utility;

public abstract class FeatureSettings<T>(Logger? logger = null, string? label = null) : ModSettings
	where T : struct, Enum {
	protected static readonly Dictionary<T, HashSet<Type>> FeaturePatches = [];

	protected static readonly ulong All = Enum.GetValues(typeof(T)).Cast<ulong>().Aggregate(0UL, (a, b) => a | b);

	protected ISet<Type> AppliedPatches = new HashSet<Type>();

	protected readonly string ConfigLabel = label ?? "disabledFeatures";

	protected readonly Logger? Logger = logger;

	private ulong _disabledFeatures;

	public T Features {
		get => FromUlong(~_disabledFeatures & All);
		set => _disabledFeatures = ~ToUlong(value) & All;
	}

	public T DisabledFeatures {
		get => FromUlong(_disabledFeatures);
		set => _disabledFeatures = ToUlong(value);
	}

	protected abstract Harmony Harmony { get; }

	public bool this[T feature] {
		get => this[ToUlong(feature)];
		set => this[ToUlong(feature)] = value;
	}

	protected bool this[ulong feature] {
		get => (~_disabledFeatures & feature) == feature;
		set {
			if (value)
				_disabledFeatures &= ~feature;
			else
				_disabledFeatures |= feature;
		}
	}

	public static void AddFeaturePatches(T feature, params Type[] patchTypes) {
		if (!FeaturePatches.TryGetValue(feature, out var list))
			FeaturePatches[feature] = list = [];
		list.AddRange(patchTypes);
	}

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref _disabledFeatures, ConfigLabel);
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

	public virtual void DrawContents(Listing_Standard listing) {
		var allFeatures = Enum.GetValues(typeof(T)).Cast<T>().ToArray();
		ulong updated = 0;
		foreach (var feature in allFeatures) {
			if (!feature.IsSingleBitFlag)
				continue;
			string? label = feature.Label ?? feature.Name;
			bool enabled = this[feature];
			listing.CheckboxLabeled(label, ref enabled);
			if (!enabled)
				updated |= ToUlong(feature);
		}

		if (updated != _disabledFeatures) {
			_disabledFeatures = updated;
			Apply();
		}
	}

	public void DrawContents(Rect inRect) {
		var listing = new Listing_Standard();
		listing.Begin(inRect);
		DrawContents(listing);
		listing.End();
	}

	protected static ulong ToUlong(T value) => (ulong)Convert.ChangeType(value, typeof(ulong));

	protected static T FromUlong(ulong value) => (T)Enum.ToObject(typeof(T), value);

	protected ISet<Type> GetPatchTypes() {
		var collection = new HashSet<Type>();
		foreach (var (feature, types) in FeaturePatches) {
			if (this[feature])
				collection.UnionWith(types);
		}
		return collection;
	}
}