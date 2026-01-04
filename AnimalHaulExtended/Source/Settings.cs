using System;
using HarmonyLib;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;

namespace TrueMogician.RimWorld.AnimalHaulExtended;

[Flags]
[FeaturesEnum("AnimalHaulExtended.Settings", true)]
public enum Features : ulong {
	None = 0
}

public class Settings() : FeatureSettings<Features>(Helper.Logger) {
	static Settings() { }

	public static Settings Default { get; internal set; } = null!;

	protected override Harmony Harmony { get; } = new(ThisAssembly.Project.PackageId);
}