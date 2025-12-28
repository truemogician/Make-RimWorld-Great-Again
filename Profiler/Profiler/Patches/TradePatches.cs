using System.Diagnostics;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Utility;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace TrueMogician.RimWorld.Profiler.Patches;

using static Helper;
using static Formatter;

[HarmonyPatch(typeof(TradeDeal))]
public static class TradePatches {
	internal static readonly Profiler Profiler = new(nameof(TradeDeal)) {
		{ typeof(TradeUtility), nameof(TradeUtility.PlayerSellableNow) },
		{ typeof(TradeUtility), nameof(TradeUtility.EverPlayerSellable) },
		{ typeof(TradeDeal), "InSellablePosition" },
		{ typeof(TransferableUtility), nameof(TransferableUtility.TradeableMatching) }
	};

	static TradePatches() {
		Profiler.Patch();
	}

	[HarmonyPatch(nameof(TradeDeal.Reset))]
	[HarmonyPrefix]
	public static void Reset_Prefix(out long __state) {
		Profiler.Reset();
		__state = Stopwatch.GetTimestamp();
	}

	[HarmonyPatch(nameof(TradeDeal.Reset))]
	[HarmonyPostfix]
	public static void Reset_Postfix(long __state) {
		var time = (Stopwatch.GetTimestamp() - __state) * 1000L / Stopwatch.Frequency;
		if (!Profiler.Log())
			return;
		Logger.Message($"TradeDeal.Reset: {Colored(time.ToString("F2"), Color.green)} ms");
	}
}