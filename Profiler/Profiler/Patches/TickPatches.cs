using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TrueMogician.RimWorld.Profiler.Windows;
using Verse;

// ReSharper disable InconsistentNaming

namespace TrueMogician.RimWorld.Profiler.Patches;

using KeyedRecords = Dictionary<string, ProfilerRecord>;
using TypedRecords = Dictionary<Type, ProfilerRecord>;

public static class TickPatches {
	internal static readonly List<SingleTickRecord> AllRecords = [];

	internal static KeyedRecords CurrentKeyedRecords = [];

	internal static TypedRecords CurrentTypedRecords = [];

	internal static bool Enabled;

	internal static long TickStarted;

	public static void Reset() {
		AllRecords.Clear();
		CurrentKeyedRecords = [];
		CurrentTypedRecords = [];
		TickStarted = 0;
	}

	public static void Start() => Enabled = true;

	public static void Stop() => Enabled = false;

	public static void Report() {
		if (AllRecords.Count == 0) {
			Find.WindowStack.Add(new Dialog_MessageBox("Tick profiler has no captured records in this session."));
			return;
		}
		var snapshot = AllRecords.ToArray();
		Find.WindowStack.Add(new TickProfilerReportWindow(snapshot));
	}

	[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
	[HarmonyPrefix]
	private static void TickManager_DoSingleTick_Prefix() {
		if (Enabled)
			TickStarted = Stopwatch.GetTimestamp();
	}

	[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
	[HarmonyFinalizer]
	private static void TickManager_DoSingleTick_Finalizer() {
		if (TickStarted != 0) {
			var record = new SingleTickRecord {
				Time = Stopwatch.GetTimestamp() - TickStarted,
				KeyedRecords = CurrentKeyedRecords,
				TypedRecords = CurrentTypedRecords
			};
			AllRecords.Add(record);
			CurrentKeyedRecords = [];
			CurrentTypedRecords = [];
			TickStarted = 0;
		}
	}

	[HarmonyPatch(typeof(Thing), nameof(Thing.DoTick))]
	[HarmonyPrefix]
	private static void Thing_DoTick_Prefix(ref long __state) {
		if (!Enabled || TickStarted == 0)
			return;
		__state = Stopwatch.GetTimestamp();
	}

	[HarmonyPatch(typeof(Thing), nameof(Thing.DoTick))]
	[HarmonyFinalizer]
	private static void Thing_DoTick_Finalizer(Thing __instance, long __state) {
		if (!Enabled || TickStarted == 0)
			return;
		long time = Stopwatch.GetTimestamp() - __state;
		var type = __instance.GetType();
		if (!CurrentTypedRecords.TryGetValue(type, out var tr))
			CurrentTypedRecords[type] = tr = new ProfilerRecord(type.FullName!);
		tr.Increment(time);
		string? key = GetKey(__instance);
		if (key is not null) {
			if (!CurrentKeyedRecords.TryGetValue(key, out var kr))
				CurrentKeyedRecords[key] = kr = new ProfilerRecord(key);
			kr.Increment(time);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string? GetKey(Thing thing) {
		return thing switch {
			Pawn human when human.RaceProps.Humanlike => $"Human: {human.Name?.ToStringFull ?? human.ThingID}",
			Pawn animal  when animal.RaceProps.Animal    => $"Animal: {animal.KindLabel} ({animal.Name?.ToStringFull ?? animal.ThingID})",
			_                                       => null
		};
	}
}

public record SingleTickRecord {
	public long Time { get; init; }

	public TypedRecords TypedRecords { get; init; } = [];

	public KeyedRecords KeyedRecords { get; init; } = [];
}