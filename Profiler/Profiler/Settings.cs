using System;
using HarmonyLib;
using TrueMogician.RimWorld.Profiler.Patches;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;

namespace TrueMogician.RimWorld.Profiler;

[Flags]
public enum ProfileTargets : ulong {
	None = 0,

	[Label("Trade Deal")]
	TradeDeal = 1 << 0,
}

public class Settings() : FeatureSettings<ProfileTargets>(Helper.Logger, "disabledTargets") {
	static Settings() {
		AddFeaturePatches(ProfileTargets.TradeDeal, typeof(TradePatches));
	}

	public static Settings Default { get; internal set; } = null!;

	protected override Harmony Harmony { get; } = new(ThisAssembly.Project.PackageId);
}