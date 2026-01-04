using System;
using HarmonyLib;
using TrueMogician.RimWorld.Rimfined.Patches;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;

namespace TrueMogician.RimWorld.Rimfined;

[Flags]
[FeaturesEnum(true)]
[Translation("Rimfined.Settings.Features", ImplicitMembers = true)]
public enum Features : ulong {
	None = 0,

	NoTarget = 1 << 0
}

public class Settings() : FeatureSettings<Features>(Helper.Logger) {
	static Settings() {
		AddFeaturePatches(Features.NoTarget, typeof(NoTargetPatches));
	}

	public static Settings Default { get; internal set; } = null!;

	protected override Harmony Harmony { get; } = new(ThisAssembly.Project.PackageId);
}