using System;
using HarmonyLib;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;
using TrueMogician.RimWorld.VehicleFrameworkFixes.Patches;

namespace TrueMogician.RimWorld.VehicleFrameworkFixes;

[Flags]
[FeaturesEnum]
[Translation("VehicleFrameworkFixes.Settings.Features", ImplicitMembers = true)]
public enum Features : ulong {
	None = 0,

	PendingPassenger = 1 << 0
}

[Translation("VehicleFrameworkFixes.Settings")]
public class Settings() : FeatureSettings<Features>(Helper.Logger) {
	static Settings() {
		AddFeaturePatches(Features.PendingPassenger, typeof(PendingPassengerPatches));
	}

	public static Settings Default { get; internal set; } = null!;

	protected override Harmony Harmony { get; } = new(ThisAssembly.Project.PackageId);
}