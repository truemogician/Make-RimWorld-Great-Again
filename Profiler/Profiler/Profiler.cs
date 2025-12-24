using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using HarmonyLib;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace TrueMogician.RimWorld.Profiler;

using static Utility.Formatter;

public class Profiler(string id) : IEnumerable<ProfilerRecord> {
	private static readonly List<ProfilerRecord> _records = [];

	private static readonly Dictionary<MethodBase, int> _methodIndices = new();

	private static readonly MethodInfo _prefixMethod = typeof(Profiler).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic)!;

	private static readonly MethodInfo _postfixMethod = typeof(Profiler).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic)!;

	private readonly Harmony _harmony = new($"{ThisAssembly.Project.PackageId}.{id}");

	private readonly Dictionary<int, (MethodBase, HarmonyMethod, HarmonyMethod)> _patches = [];

	public IEnumerator<ProfilerRecord> GetEnumerator() {
		lock (_records) {
			foreach (var index in _patches.Keys)
				yield return _records[index];
		}
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public string Id { get; } = id;

	public void Add(MethodBase method) {
		int index;
		lock (_records) {
			index = _records.Count;
			_records.Add(new ProfilerRecord(method.Name));
			_methodIndices[method] = index;
		}
		_patches.Add(index, (method, _prefixMethod, _postfixMethod));
	}

	public void Add(Type type, string methodName, Type[]? parameters = null, Type[]? generics = null)
		=> Add(AccessTools.Method(type, methodName, parameters, generics));

	public void Patch() {
		foreach (var (method, prefix, postfix) in _patches.Values)
			_harmony.Patch(method, prefix, postfix);
	}

	public void Unpatch() {
		_harmony.UnpatchAll(_harmony.Id);
	}

	public void Reset() {
		lock (_records) {
			foreach (var index in _patches.Keys)
				_records[index].Reset();
		}
	}

	public bool Log(Color? labelColor = null, Color? numberColor = null) {
		var records = this.ToArray();
		if (records.Length == 0)
			return false;
		labelColor ??= Color.cyan;
		numberColor ??= Color.green;
		var sb = new StringBuilder();
		Verse.Log.Message($"Profiler {Colored(Id, labelColor)}:");
		foreach ((string label, int count, long ticks) in records) {
			var time = (double)ticks * 1000 / Stopwatch.Frequency;
			sb.Append($"  {Colored(label, labelColor)}: ");
			sb.Append($"Count={Colored(count, numberColor)}, ");
			sb.Append($"Time={Colored($"{time:F2}ms", numberColor)}, ");
			if (count > 0)
				sb.Append($"Average={Colored($"{time * 1000 / count:F2}μs", numberColor)}");
			Verse.Log.Message(sb.ToString());
			sb.Clear();
		}
		return true;
	}

	private static void Prefix(out long __state) {
		__state = Stopwatch.GetTimestamp();
	}

	private static void Postfix(MethodBase __originalMethod, long __state) {
		var end = Stopwatch.GetTimestamp();
		var ticks = end - __state;
		var index = _methodIndices[__originalMethod];
		// ReSharper disable once InconsistentlySynchronizedField
		_records[index].Increment(ticks);
	}
}

public record ProfilerRecord(string Label, int Count = 0, long Ticks = 0) {
	private int _count = Count;

	private long _ticks = Ticks;

	public string Label { get; } = Label;

	public int Count => _count;

	public long Ticks => _ticks;

	public double AverageTicks => Count == 0 ? 0 : (double)Ticks / Count;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Increment(long ticks) {
		Interlocked.Increment(ref _count);
		Interlocked.Add(ref _ticks, ticks);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Reset() {
		_count = 0;
		_ticks = 0;
	}
}