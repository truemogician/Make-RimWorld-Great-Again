using System.Diagnostics;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace TrueMogician.RimWorld.Profiler.Patches;

using static Helper;
using static Formatter;

[HarmonyPatch(typeof(TradeDeal))]
internal static class TradePatches {
	internal static readonly Profiler Profiler = new(nameof(TradeDeal)) {
		{ typeof(TradeUtility), nameof(TradeUtility.PlayerSellableNow) },
		{ typeof(TradeUtility), nameof(TradeUtility.EverPlayerSellable) },
		{ typeof(TradeDeal), "InSellablePosition" },
		{ typeof(TransferableUtility), nameof(TransferableUtility.TradeableMatching) }
	};

	[PatchHook(PatchHookTiming.AfterPatch)]
	internal static void AfterPatch() => Profiler.Patch();

	[PatchHook(PatchHookTiming.BeforeUnpatch)]
	internal static void BeforeUnpatch() => Profiler.Unpatch();

	[HarmonyPatch(nameof(TradeDeal.Reset))]
	[HarmonyPrefix]
	internal static void Reset_Prefix(out long __state) {
		Profiler.Reset();
		__state = Stopwatch.GetTimestamp();
	}

	[HarmonyPatch(nameof(TradeDeal.Reset))]
	[HarmonyPostfix]
	internal static void Reset_Postfix(long __state) {
		var time = (Stopwatch.GetTimestamp() - __state) * 1000L / Stopwatch.Frequency;
		if (!Profiler.Log())
			return;
		Logger.Message($"TradeDeal.Reset: {Colored(time.ToString("F2"), Color.green)} ms");
	}
}