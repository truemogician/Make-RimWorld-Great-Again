using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Verse;

namespace TrueMogician.RimWorld.Utility.Diagnostics;

public enum Verbosity : byte {
	Off = 0,

	Summary = 1,

	Full = 2
}

/// <summary>
///     A single diagnostic event captured at an instrumented site.
/// </summary>
public sealed record DiagnosticEvent(
	int Tick,
	string Category,
	string Tag,
	int? PawnId = null,
	string? PawnLabel = null,
	int? ThingId = null,
	string? ThingLabel = null,
	IntVec3? Cell = null,
	string? Details = null
);

public interface IDiagnosticSink {
	void Record(DiagnosticEvent ev);

	void Flush();
}

/// <summary>
///     Static facade for opt-in diagnostic event capture.
/// </summary>
public static class Diagnostic {
	private static readonly List<IDiagnosticSink> _sinks = [];

	private static readonly object _sinksLock = new();

	public static Verbosity Level;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsEnabled(Verbosity minimum) => Level >= minimum;

	public static void AddSink(IDiagnosticSink sink) {
		lock (_sinksLock) {
			if (!_sinks.Contains(sink))
				_sinks.Add(sink);
		}
	}

	public static void RemoveSink(IDiagnosticSink sink) {
		lock (_sinksLock)
			_sinks.Remove(sink);
	}

	public static void ClearSinks() {
		lock (_sinksLock)
			_sinks.Clear();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Record(string category, string tag, string? details = null, Verbosity minimum = Verbosity.Summary) =>
		RecordPrivate(category, tag, details: details, minimum: minimum);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Record(string category, string tag, Pawn? pawn, string? details = null, Verbosity minimum = Verbosity.Summary) =>
		RecordPrivate(category, tag, pawn?.thingIDNumber, pawn?.LabelShort, details: details, minimum: minimum);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Record(string category, string tag, Pawn? pawn, Thing? thing, string? details = null, Verbosity minimum = Verbosity.Summary) =>
		RecordPrivate(category, tag, pawn?.thingIDNumber, pawn?.LabelShort, thing?.thingIDNumber, thing?.def?.defName, null, details, minimum);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Record(
		string category,
		string tag,
		Pawn? pawn,
		Thing? thing,
		IntVec3? cell,
		string? details = null,
		Verbosity minimum = Verbosity.Summary
	) => RecordPrivate(category, tag, pawn?.thingIDNumber, pawn?.LabelShort, thing?.thingIDNumber, thing?.def?.defName, cell, details, minimum);

	public static void FlushAll() {
		lock (_sinksLock) {
			foreach (var sink in _sinks) {
				try {
					sink.Flush();
				}
				catch (Exception e) {
					Log.Warning($"[Diagnostic] Sink {sink.GetType().Name} threw during Flush: {e.GetType().Name} {e.Message}");
				}
			}
		}
	}

	private static void RecordPrivate(
		string category,
		string tag,
		int? pawnId = null,
		string? pawnLabel = null,
		int? thingId = null,
		string? thingLabel = null,
		IntVec3? cell = null,
		string? details = null,
		Verbosity minimum = Verbosity.Summary
	) {
		if (Level < minimum)
			return;
		int tick = Current.Game is null ? 0 : Find.TickManager?.TicksGame ?? 0;
		var ev = new DiagnosticEvent(tick, category, tag, pawnId, pawnLabel, thingId, thingLabel, cell, details);
		lock (_sinksLock) {
			foreach (var sink in _sinks) {
				try {
					sink.Record(ev);
				}
				catch (Exception e) {
					Log.Warning($"[Diagnostic] Sink {sink.GetType().Name} threw during Record: {e.GetType().Name} {e.Message}");
				}
			}
		}
	}
}