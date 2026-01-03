using System;
using HarmonyLib;
using TrueMogician.RimWorld.Rimsonable.Patches;
using TrueMogician.RimWorld.Rimsonable.Static;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;

namespace TrueMogician.RimWorld.Rimsonable;

[Flags]
[FeaturesEnum("Rimsonable.Settings.Features", true)]
public enum Features : ulong {
	None = 0,

	AllowGrenadesThroughShields = 1 << 0,

	SafeRestLocation = 1 << 1
}

public class Settings() : FeatureSettings<Features>(Helper.Logger) {
	static Settings() {
		AddFeaturePatches(Features.AllowGrenadesThroughShields, typeof(CompShieldPatches));
		AddFeaturePatches(Features.SafeRestLocation, typeof(RestLocationPatches));
	}

	public static Settings Default { get; internal set; } = null!;

	protected override Harmony Harmony { get; } = new(ThisAssembly.Project.PackageId);
}