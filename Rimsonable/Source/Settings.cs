using System;
using HarmonyLib;
using TrueMogician.RimWorld.Rimsonable.Patches;
using TrueMogician.RimWorld.Rimsonable.Static;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace TrueMogician.RimWorld.Rimsonable;

[Flags]
public enum Features : ulong {
	None = 0,

	[Label("Allow Grenades Through Shields")]
	[Description("Allows grenades to pass through shield bubbles.")]
	AllowGrenadesThroughShields = 1 << 0,
}

public class Settings() : FeatureSettings<Features>(Helper.Logger, "disabledFeatures") {
	static Settings() {
		AddFeaturePatches(Features.AllowGrenadesThroughShields, typeof(CompShieldPatches));
	}

	public static Settings Default { get; internal set; } = null!;

	protected override Harmony Harmony { get; } = new(ThisAssembly.Project.PackageId);
}