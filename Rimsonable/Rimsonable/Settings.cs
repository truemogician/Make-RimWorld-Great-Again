using System;
using System.Collections.Generic;
using TrueMogician.RimWorld.Rimsonable.Patches;
using TrueMogician.RimWorld.Utility.Attributes;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable;

[Flags]
public enum Features : ulong {
	None = 0,

	[Label("Allow Grenades Through Shields")]
	[Description("Allows grenades to pass through shield bubbles.")]
	AllowGrenadesThroughShields = 1 << 0,

	All = ulong.MaxValue
}

public class Settings : ModSettings {
	private static readonly Dictionary<Features, List<Type>> FeaturePatches = new() {
		{ Features.AllowGrenadesThroughShields, [typeof(CompShieldPatches)] }
	};

	private Features _features = Features.All;

	public static Settings Default { get; internal set; } = null!;

	public Features Features {
		get => _features;
		internal set => _features = value;
	}

	public bool this[Features feature] {
		get => (_features & feature) == feature;
		internal set {
			if (value)
				_features |= feature;
			else
				_features &= ~feature;
		}
	}

	public static void AddFeaturePatches(Features feature, params Type[] patchTypes) {
		if (!FeaturePatches.TryGetValue(feature, out var list))
			FeaturePatches[feature] = list = [];
		list.AddRange(patchTypes);
	}

	internal IReadOnlyList<Type> GetPatchTypes() {
		var collection = new List<Type>();
		foreach (var (feature, types) in FeaturePatches) {
			if (this[feature])
				collection.AddRange(types);
		}
		return collection;
	}

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref _features, "featureFlags", Features.All);
	}

	public void Apply() {
		// Placeholder for any future application logic
	}
}