using System;
using HarmonyLib;
using TrueMogician.RimWorld.RemedialAlgorithms.Patches;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;

namespace TrueMogician.RimWorld.RemedialAlgorithms;

[Flags]
[FeaturesEnum(true)]
[Translation("RemedialAlgorithms.Settings", ImplicitMembers = true)]
public enum Optimizations : ulong {
	None = 0,

	TradeSetup = 1 << 0,
}

public class Settings() : FeatureSettings<Optimizations>(Helper.Logger) {
	static Settings() {
		AddFeaturePatches(Optimizations.TradeSetup, typeof(TradeDealPatches));
	}

	public static Settings Default { get; internal set; } = null!;

	protected override Harmony Harmony { get; } = new(ThisAssembly.Project.PackageId);
}