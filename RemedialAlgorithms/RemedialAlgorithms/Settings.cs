using System;
using HarmonyLib;
using TrueMogician.RimWorld.RemedialAlgorithms.Patches;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;

namespace TrueMogician.RimWorld.RemedialAlgorithms;

[Flags]
public enum Optimizations : ulong {
	None = 0,

	[Label("Trade Iteration")]
	TradeIteration = 1 << 0,
}

public class Settings() : FeatureSettings<Optimizations>(Helper.Logger, "disabledOptimizations") {
	static Settings() {
		AddFeaturePatches(Optimizations.TradeIteration, typeof(TradeDealPatches));
	}

	public static Settings Default { get; internal set; } = null!;

	protected override Harmony Harmony { get; } = new(ThisAssembly.Project.PackageId);
}