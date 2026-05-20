using System;
using HarmonyLib;
using TrueMogician.RimWorld.ExactStorage.Patches;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;
using TrueMogician.RimWorld.Utility.Diagnostics;

namespace TrueMogician.RimWorld.ExactStorage;

[Flags]
[FeaturesEnum]
[Translation("ExactStorage.Settings.Features", ImplicitMembers = true)]
public enum Features : ulong {
	None = 0,

	[FeatureIgnore]
	Core = 1 << 0,

	[Feature(DefaultEnabled = false)]
	Diagnostics = 1 << 1
}

[Translation("ExactStorage.Settings")]
public class Settings() : FeatureSettings<Features>(Helper.Logger) {
	static Settings() {
		AddFeaturePatches(
			Features.Core,
			typeof(StorageBehaviorPatches),
			typeof(StorageSettingsPatches),
			typeof(StorageUIPatches)
		);
	}

	public static Settings Default { get; internal set; } = null!;

	protected override Harmony Harmony { get; } = new(ThisAssembly.Project.PackageId);

	public override void Apply() {
		base.Apply();
		Diagnostic.Level = this[Features.Diagnostics] ? Verbosity.Full : Verbosity.Off;
	}
}