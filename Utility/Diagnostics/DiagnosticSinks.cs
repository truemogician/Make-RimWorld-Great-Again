using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Verse;

namespace TrueMogician.RimWorld.Utility.Diagnostics;

/// <summary>
///     Emits each diagnostic event as a single tab-separated line through a <see cref="Utility.Logger" />.
/// </summary>
public sealed class LogSink(Logger logger) : IDiagnosticSink {
	private readonly Logger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	public void Record(DiagnosticEvent ev) => _logger.Message(Format(ev));

	public void Flush() { }

	private static string Format(DiagnosticEvent ev) {
		var sb = new StringBuilder();
		sb.Append("tick=").Append(ev.Tick.ToString(CultureInfo.InvariantCulture))
			.Append("\tcat=").Append(ev.Category)
			.Append("\ttag=").Append(ev.Tag);
		if (ev.PawnId.HasValue)
			sb.Append("\tpawn=").Append(ev.PawnLabel ?? "?").Append('#').Append(ev.PawnId.Value.ToString(CultureInfo.InvariantCulture));
		if (ev.ThingId.HasValue)
			sb.Append("\tthing=").Append(ev.ThingLabel ?? "?").Append('#').Append(ev.ThingId.Value.ToString(CultureInfo.InvariantCulture));
		if (ev.Cell.HasValue) {
			var c = ev.Cell.Value;
			sb.Append("\tcell=(")
				.Append(c.x.ToString(CultureInfo.InvariantCulture)).Append(',')
				.Append(c.y.ToString(CultureInfo.InvariantCulture)).Append(',')
				.Append(c.z.ToString(CultureInfo.InvariantCulture)).Append(')');
		}
		if (!string.IsNullOrEmpty(ev.Details))
			sb.Append('\t').Append(ev.Details);
		return sb.ToString();
	}
}

/// <summary>
///     Retains the most recent <c>Capacity</c> events in a thread-safe ring buffer.
///     Useful when you want to wait for an external trigger before deciding to dump context.
/// </summary>
public sealed class RingBufferSink : IDiagnosticSink {
	private readonly DiagnosticEvent[] _buffer;
	private readonly object _lock = new();
	private int _head;
	private int _count;

	public RingBufferSink(int capacity) {
		if (capacity <= 0)
			throw new ArgumentOutOfRangeException(nameof(capacity));
		_buffer = new DiagnosticEvent[capacity];
	}

	public int Capacity => _buffer.Length;

	public int Count {
		get {
			lock (_lock) {
				return _count;
			}
		}
	}

	public void Record(DiagnosticEvent ev) {
		lock (_lock) {
			_buffer[_head] = ev;
			_head = (_head + 1) % _buffer.Length;
			if (_count < _buffer.Length)
				_count++;
		}
	}

	public void Flush() { }

	public IReadOnlyList<DiagnosticEvent> Snapshot() {
		lock (_lock) {
			var result = new DiagnosticEvent[_count];
			int start = (_head - _count + _buffer.Length) % _buffer.Length;
			for (var i = 0; i < _count; i++)
				result[i] = _buffer[(start + i) % _buffer.Length];
			return result;
		}
	}

	public void Drain(IDiagnosticSink target) {
		var snap = Snapshot();
		foreach (var ev in snap)
			target.Record(ev);
		target.Flush();
		lock (_lock) {
			_head = 0;
			_count = 0;
		}
	}
}

/// <summary>
///     Wraps another sink, suppressing identical events keyed by
///     <c>(tick, category, tag, pawnId, thingId, cell)</c>. Within one tick a loop iterating the same call site only
///     records one event per unique key, keeping logs readable when a hot path fires thousands of times per tick.
/// </summary>
public sealed class RateLimitedSink(IDiagnosticSink inner) : IDiagnosticSink {
	private readonly IDiagnosticSink _inner = inner ?? throw new ArgumentNullException(nameof(inner));
	private readonly HashSet<Key> _seen = [];
	private readonly object _lock = new();
	private int _currentTick = int.MinValue;

	public void Record(DiagnosticEvent ev) {
		lock (_lock) {
			if (ev.Tick != _currentTick) {
				_currentTick = ev.Tick;
				_seen.Clear();
			}
			if (!_seen.Add(new Key(ev.Category, ev.Tag, ev.PawnId, ev.ThingId, ev.Cell)))
				return;
		}
		_inner.Record(ev);
	}

	public void Flush() => _inner.Flush();

	private readonly record struct Key(string Category, string Tag, int? PawnId, int? ThingId, IntVec3? Cell);
}
