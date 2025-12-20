using System;
using System.Collections.Generic;
using HarmonyLib;
using TrueMogician.RimWorld.Rimsonable.Patches;
using TrueMogician.RimWorld.Rimsonable.Static;
using TrueMogician.RimWorld.Utility.Attributes;
using Verse;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

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

	private IReadOnlyList<Type> _appliedPatches = [];

	public static Settings Default { get; internal set; } = null!;

	internal Harmony Harmony { get; } = new Harmony(ThisAssembly.Project.PackageId);

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
		if (_appliedPatches.Count > 0) {
			Harmony.UnpatchAll();
			Helper.Logger.Message($"Removed {_appliedPatches.Count} patches");
		}
		_appliedPatches = GetPatchTypes();
		foreach (var patchType in _appliedPatches)
			Harmony.PatchAll(patchType.Assembly);
		Helper.Logger.Message($"Applied {_appliedPatches.Count} patches");
	}
}