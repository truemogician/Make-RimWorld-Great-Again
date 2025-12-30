using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Verse;

// ReSharper disable InconsistentNaming

namespace TrueMogician.RimWorld.Profiler.Patches;

using KeyedRecords = Dictionary<string, ProfilerRecord>;
using TypedRecords = Dictionary<Type, ProfilerRecord>;

public static class TickPatches {
	internal static readonly object RecordsLock = new();

	internal static readonly List<SingleTickRecord> AllRecords = [];

	internal static readonly TickProfilerSummary Summary = new();

	internal static KeyedRecords CurrentKeyedRecords = [];

	internal static TypedRecords CurrentTypedRecords = [];

	internal static bool Enabled;

	internal static long TickStarted;

	public static void Reset() {
		lock (RecordsLock) {
			AllRecords.Clear();
			CurrentKeyedRecords = [];
			CurrentTypedRecords = [];
			Summary.Reset();
			TickStarted = 0;
		}
	}

	public static void Start() => Enabled = true;

	public static void Stop() => Enabled = false;

	[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
	[HarmonyPrefix]
	private static void TickManager_DoSingleTick_Prefix() {
		TickStarted = Enabled ? Stopwatch.GetTimestamp() : 0L;
	}

	[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
	[HarmonyFinalizer]
	private static void TickManager_DoSingleTick_Finalizer() {
		if (TickStarted != 0) {
			var time = Stopwatch.GetTimestamp() - TickStarted;
			var record = new SingleTickRecord {
				Time = time,
				KeyedRecords = CurrentKeyedRecords,
				TypedRecords = CurrentTypedRecords
			};
			lock (RecordsLock) {
				AllRecords.Add(record);
				Summary.Increment(time);
				CurrentKeyedRecords = [];
				CurrentTypedRecords = [];
				TickStarted = 0;
			}
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
		Summary.TotalTypedTime += time;
		var type = __instance.GetType();
		if (!CurrentTypedRecords.TryGetValue(type, out var tr))
			CurrentTypedRecords[type] = tr = new ProfilerRecord(type.FullName!);
		tr.Increment(time);
		string? key = GetKey(__instance);
		if (key is not null) {
			Summary.TotalKeyedTime += time;
			if (!CurrentKeyedRecords.TryGetValue(key, out var kr))
				CurrentKeyedRecords[key] = kr = new ProfilerRecord(key);
			kr.Increment(time);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string? GetKey(Thing thing) {
		return thing switch {
			Pawn human when human.RaceProps.Humanlike => $"Human: {human.Name?.ToStringFull ?? human.ThingID}",
			Pawn animal when animal.RaceProps.Animal  => $"Animal: {animal.KindLabel} ({animal.Name?.ToStringFull ?? animal.ThingID})",
			_                                         => null
		};
	}
}

public record SingleTickRecord {
	public long Time { get; init; }

	public TypedRecords TypedRecords { get; init; } = [];

	public KeyedRecords KeyedRecords { get; init; } = [];
}

public class TickProfilerSummary(
	int tickCount,
	long totalTime,
	long minTime,
	long maxTime,
	long totalTypedTime,
	long totalKeyedTime
) {
	public TickProfilerSummary() : this(0, 0L, 0L, 0L, 0L, 0L) { }

	public int TickCount { get; internal set; } = tickCount;

	public long TotalTime { get; internal set; } = totalTime;

	public long MinTime { get; internal set; } = minTime;

	public long MaxTime { get; internal set; } = maxTime;

	public long TotalTypedTime { get; internal set; } = totalTypedTime;

	public long TotalKeyedTime { get; internal set; } = totalKeyedTime;

	public double AvgTime => TickCount == 0 ? 0 : TotalTime / (double)TickCount;

	public double TypedPercent => TotalTime == 0 ? 0 : TotalTypedTime * 100 / (double)TotalTime;

	public double KeyedPercent => TotalTime == 0 ? 0 : TotalKeyedTime * 100 / (double)TotalTime;

	public void Reset() {
		TickCount = 0;
		TotalTime = 0L;
		MinTime = 0L;
		MaxTime = 0L;
		TotalTypedTime = 0L;
		TotalKeyedTime = 0L;
	}

	public void Increment(long time) {
		++TickCount;
		TotalTime += time;
		if (TickCount == 1) {
			MinTime = time;
			MaxTime = time;
		}
		else {
			if (time < MinTime)
				MinTime = time;
			if (time > MaxTime)
				MaxTime = time;
		}
	}
}