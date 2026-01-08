using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using Verse;

// ReSharper disable InconsistentNaming

namespace TrueMogician.RimWorld.Profiler.Patches;

using ThingIdProfilerRecord = ProfilerRecord<int>;
using ThingProfilerRecord = ProfilerRecord<Thing>;

public static class TickPatches {
	internal static readonly object RecordsLock = new();

	internal static readonly List<SingleTickRecord> AllRecords = [];

	internal static readonly TickProfilerSummary Summary = new();

	internal static Dictionary<int, ThingProfilerRecord> ThingRecords = new();

	internal static bool Enabled;

	internal static long TickStarted;

	public static void Reset() {
		lock (RecordsLock) {
			AllRecords.Clear();
			ThingRecords = new Dictionary<int, ThingProfilerRecord>();
			Summary.Reset();
			TickStarted = 0;
		}
	}

	public static void Start() => Enabled = true;

	public static void Stop() => Enabled = false;

	[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPrefix]
	private static void TickManager_DoSingleTick_Prefix() {
		TickStarted = Enabled ? Stopwatch.GetTimestamp() : 0L;
	}

	[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
	[HarmonyPriority(Priority.First)]
	[HarmonyFinalizer]
	private static void TickManager_DoSingleTick_Finalizer() {
		if (TickStarted == 0)
			return;
		var time = Stopwatch.GetTimestamp() - TickStarted;
		var record = new SingleTickRecord(time, ThingRecords.Values);
		lock (RecordsLock) {
			AllRecords.Add(record);
			Summary.Increment(time);
			ThingRecords = new Dictionary<int, ThingProfilerRecord>();
			TickStarted = 0;
		}
	}

	[HarmonyPatch(typeof(Thing), nameof(Thing.DoTick))]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPrefix]
	private static void Thing_DoTick_Prefix(ref long __state) {
		if (!Enabled || TickStarted == 0)
			return;
		__state = Stopwatch.GetTimestamp();
	}

	[HarmonyPatch(typeof(Thing), nameof(Thing.DoTick))]
	[HarmonyPriority(Priority.First)]
	[HarmonyPostfix]
	private static void Thing_DoTick_Postfix(Thing __instance, long __state) {
		if (!Enabled || TickStarted == 0)
			return;
		long time = Stopwatch.GetTimestamp() - __state;
		var id = __instance.thingIDNumber;
		if (!ThingRecords.TryGetValue(id, out var record))
			record = new ThingProfilerRecord(__instance);
		record.Increment(time);
		ThingRecords[id] = record;
	}
}

public class PawnPropsProfilerComparer : IEqualityComparer<PawnProps> {
	public static PawnPropsProfilerComparer Instance { get; } = new();

	public bool Equals(PawnProps x, PawnProps y) {
		if (!x.OnActiveMap && !y.OnActiveMap)
			return true;
		if (x.State != y.State)
			return false;
		if (x.State != PawnState.Active)
			return true;
		return x.Type == y.Type;
	}

	public int GetHashCode(PawnProps obj) {
		if (!obj.OnActiveMap)
			return 1 << 16;
		if (obj.State != PawnState.Active)
			return (byte)obj.State << 8;
		return (byte)obj.Type;
	}
}

public record SingleTickRecord {
	public enum Category : byte {
		Type,
		Pawn,
		Keyed
	}

	public class CategoryRecord<TKey>(Category key, IEqualityComparer<TKey>? comparer = null)
		: AggProfilerRecord<Category, AggProfilerRecord<TKey, ThingIdProfilerRecord, int>, TKey>(key, comparer);

	public SingleTickRecord(long totalQpcTicks, IEnumerable<ThingProfilerRecord> records) {
		QpcTicks = totalQpcTicks;
		foreach ((var key, int hitCount, long qpcTicks) in records) {
			var idRecord = new ThingIdProfilerRecord(key.thingIDNumber, hitCount, qpcTicks);
			var type = key.GetType();
			if (!TypeRecord.TryGetValue(type, out var rt))
				TypeRecord.Add(rt = new(type));
			rt.Add(idRecord);
			if (key is Pawn pawn) {
				var props = new PawnProps(pawn);
				if (!PawnRecord.TryGetValue(props, out var rp))
					PawnRecord.Add(rp = new(props));
				rp.Add(idRecord);
			}
			if (GetThingKey(key) is { } keyString) {
				if (!KeyedRecord.TryGetValue(keyString, out var rk))
					KeyedRecord.Add(rk = new(keyString));
				rk.Add(idRecord);
			}
		}
	}

	public long QpcTicks { get; init; }

	public CategoryRecord<Type> TypeRecord { get; } = new(Category.Type);

	public CategoryRecord<PawnProps> PawnRecord { get; } = new(Category.Pawn, PawnPropsProfilerComparer.Instance);

	public CategoryRecord<string> KeyedRecord { get; } = new(Category.Keyed);

	public static string? GetThingKey(Thing thing) {
		if (thing is not Pawn { IsFreeNonSlaveColonist: true, Name: { } name })
			return null;
		return name.ToStringFull;
	}
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