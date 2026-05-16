using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Verse;

namespace TrueMogician.RimWorld.Utility.Diagnostics;

/// <summary>
///     RAII stopwatch that emits a <c>Diagnostic</c> event with the elapsed microseconds on disposal. The constructor
///     short-circuits when verbosity is below the configured threshold, so when diagnostics are off the only cost is a
///     single field-compare-and-branch.
/// </summary>
public readonly ref struct ScopedTimer {
	private readonly long _start;
	private readonly string? _category;
	private readonly string? _tag;
	private readonly Pawn? _pawn;
	private readonly Thing? _thing;
	private readonly IntVec3? _cell;
	private readonly string? _details;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ScopedTimer(
		string category,
		string tag,
		Verbosity minimum = Verbosity.Full,
		Pawn? pawn = null,
		Thing? thing = null,
		IntVec3? cell = null,
		string? details = null
	) {
		if (Diagnostic.Level < minimum) {
			_start = 0;
			_category = null;
			_tag = null;
			_pawn = null;
			_thing = null;
			_cell = null;
			_details = null;
			return;
		}
		_start = Stopwatch.GetTimestamp();
		_category = category;
		_tag = tag;
		_pawn = pawn;
		_thing = thing;
		_cell = cell;
		_details = details;
	}

	public void Dispose() {
		if (_category is null)
			return;
		long elapsed = Stopwatch.GetTimestamp() - _start;
		double micros = elapsed * 1_000_000.0 / Stopwatch.Frequency;
		string suffix = "us=" + micros.ToString("F1", CultureInfo.InvariantCulture);
		string combined = _details is null ? suffix : _details + "\t" + suffix;
		Diagnostic.Record(_category, _tag!, _pawn, _thing, _cell, combined);
	}
}
