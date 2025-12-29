using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TrueMogician.RimWorld.Profiler.Patches;
using TrueMogician.RimWorld.Utility.GUI;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;

namespace TrueMogician.RimWorld.Profiler.Windows;

public sealed class TickProfilerReportWindow : Window {
	private const float _ROW_HEIGHT = 24f;
	private const int _MAX_LONG_TICKS_SHOWN = 10;
	private const float _SCROLLBAR_WIDTH = 16f;
	private readonly List<AggregateEntry> _keyEntries;
	private readonly List<(int Index, long Ticks)> _longTicks;
	private readonly long _maxTickTicks;
	private readonly long _minTickTicks;

	private readonly IReadOnlyList<SingleTickRecord> _records;
	private Vector2 _scrollKeys;

	private Vector2 _scrollTypes;
	private readonly long _totalKeyedTicks;
	private readonly long _totalTickTicks;
	private readonly long _totalTypedTicks;
	private readonly List<AggregateEntry> _typeEntries;

	public TickProfilerReportWindow(IEnumerable<SingleTickRecord> records) {
		_records = records as IReadOnlyList<SingleTickRecord> ?? records.ToArray();
		if (_records.Count == 0) {
			_longTicks = [];
			_typeEntries = [];
			_keyEntries = [];
		}
		else {
			_totalTickTicks = _records.Sum(r => r.Time);
			_minTickTicks = _records.Min(r => r.Time);
			_maxTickTicks = _records.Max(r => r.Time);

			_longTicks = _records
				.Select((r, i) => (Index: i + 1, Ticks: r.Time))
				.OrderByDescending(t => t.Ticks)
				.Take(_MAX_LONG_TICKS_SHOWN)
				.ToList();

			var byType = new Dictionary<string, AggregateEntry>();
			var byKey = new Dictionary<string, AggregateEntry>();

			foreach (var tick in _records) {
				foreach (var kvp in tick.TypedRecords) {
					string label = kvp.Value.Label;
					if (!byType.TryGetValue(label, out var agg))
						byType[label] = agg = new AggregateEntry(label);
					agg.Add(kvp.Value);
				}
				foreach (var kvp in tick.KeyedRecords) {
					string label = kvp.Value.Label;
					if (!byKey.TryGetValue(label, out var agg))
						byKey[label] = agg = new AggregateEntry(label);
					agg.Add(kvp.Value);
				}
			}

			_typeEntries = byType.Values.OrderByDescending(e => e.Ticks).ToList();
			_totalTypedTicks = _typeEntries.Sum(e => e.Ticks);

			_keyEntries = byKey.Values.OrderByDescending(e => e.Ticks).ToList();
			_totalKeyedTicks = _keyEntries.Sum(e => e.Ticks);
		}

		doCloseX = true;
		doCloseButton = true;
		draggable = true;
		absorbInputAroundWindow = true;
	}

	public override Vector2 InitialSize => new(1100f, 750f);

	public override void DoWindowContents(Rect inRect) {
		var rows = inRect.ToFlexbox(FlexDirection.Column, [35f, 70f, 90f, "1fr"], 5f).ToList();
		using (Scoped.Text(font: GameFont.Medium))
			Widgets.Label(rows[0], "Tick Profiler Report");
		using (Scoped.Text(font: GameFont.Small)) {
			DrawSummary(rows[1]);
			DrawLongTicks(rows[2]);
			DrawLists(rows[3]);
		}
	}

	private static void DrawAggregateTable(Rect rect, string title, List<AggregateEntry> entries, long totalTicks, ref Vector2 scrollPos) {
		Widgets.DrawMenuSection(rect);
		var inner = rect.ContractedBy(8f);

		Widgets.Label(new Rect(inner.x, inner.y, inner.width, 22f), $"{title} — {entries.Count} entries");

		float headerY = inner.y + 26f;
		var headerRect = new Rect(inner.x, headerY, inner.width, 22f);
		DrawHeaderRow(headerRect);

		var outRect = new Rect(inner.x, headerRect.yMax + 2f, inner.width, inner.yMax - (headerRect.yMax + 2f));
		float viewHeight = entries.Count * _ROW_HEIGHT;
		var viewRect = new Rect(0f, 0f, outRect.width - _SCROLLBAR_WIDTH, Math.Max(outRect.height, viewHeight));

		Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
		try {
			Text.WordWrap = false;
			for (var i = 0; i < entries.Count; i++) {
				var rowRect = new Rect(0f, i * _ROW_HEIGHT, viewRect.width, _ROW_HEIGHT);
				if (Mouse.IsOver(rowRect))
					Widgets.DrawHighlight(rowRect);
				DrawEntryRow(rowRect, entries[i], totalTicks);
			}
		}
		finally {
			Text.WordWrap = true;
			Widgets.EndScrollView();
		}
	}

	private static void DrawHeaderRow(Rect rect) {
		var cols = rect.ToFlexbox([90f, 55f, 60f, 90f, "1fr"], 6f).ToList();
		using (Scoped.Text(TextAnchor.MiddleLeft)) {
			Widgets.Label(cols[0], "Time");
			Widgets.Label(cols[1], "%");
			Widgets.Label(cols[2], "Count");
			Widgets.Label(cols[3], "Avg");
			Widgets.Label(cols[4], "Label");
		}
	}

	private static void DrawEntryRow(Rect rect, AggregateEntry entry, long totalTicks) {
		var cols = rect.ToFlexbox([90f, 55f, 60f, 90f, "1fr"], 6f).ToList();

		double timeMs = TicksToMs(entry.Ticks);
		double pct = totalTicks == 0 ? 0 : entry.Ticks * 100.0 / totalTicks;
		double avgUs = entry.Count == 0 ? 0 : entry.AverageTicks * 1_000_000.0 / Stopwatch.Frequency;

		using (Scoped.Text(TextAnchor.MiddleRight)) {
			Widgets.Label(cols[0], $"{timeMs:F2}ms");
			Widgets.Label(cols[1], $"{pct:F1}");
			Widgets.Label(cols[2], entry.Count.ToString());
			Widgets.Label(cols[3], $"{avgUs:F1}μs");
		}

		using (Scoped.Text(TextAnchor.MiddleLeft)) {
			var labelRect = cols[4];
			string truncated = entry.Label.Truncate(labelRect.width);
			Widgets.Label(labelRect, truncated);
			if (Mouse.IsOver(labelRect))
				TooltipHandler.TipRegion(labelRect, entry.Label);
		}
	}

	private static double TicksToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

	private static string TicksToMsString(long ticks) => $"{TicksToMs(ticks):F3}ms";

	private void DrawSummary(Rect rect) {
		Widgets.DrawMenuSection(rect);
		var inner = rect.ContractedBy(8f);

		int tickCount = _records.Count;
		double avgTickMs = tickCount == 0 ? 0 : TicksToMs(_totalTickTicks) / tickCount;

		string[] lines = [
			$"Ticks captured: {tickCount}\tTotal tick time: {TicksToMsString(_totalTickTicks)}\tAvg: {avgTickMs:F3}ms",
			$"Min: {TicksToMsString(_minTickTicks)}\tMax: {TicksToMsString(_maxTickTicks)}",
			$"Thing.DoTick typed total: {TicksToMsString(_totalTypedTicks)}\tkeyed total: {TicksToMsString(_totalKeyedTicks)}"
		];
		if (_totalTickTicks > 0)
			lines[2] += $"\t(typed {_totalTypedTicks * 100.0 / _totalTickTicks:F1}% of tick time)";
		var rows = inner.ToFlexbox(FlexDirection.Column, [22f, 22f, 22f]).ToArray();
		using (Scoped.Text(TextAnchor.UpperLeft, wordWrap: false)) {
			for (var i = 0; i < 3; ++i)
				Widgets.Label(rows[i], lines[i]);
		}
	}

	private void DrawLongTicks(Rect rect) {
		Widgets.DrawMenuSection(rect);
		var inner = rect.ContractedBy(8f);
		Widgets.Label(new Rect(inner.x, inner.y, inner.width, 22f), "Longest ticks (by total tick time):");
		float y = inner.y + 24f;
		if (_longTicks.Count == 0) {
			Widgets.Label(new Rect(inner.x, y, inner.width, 22f), "(none)");
			return;
		}
		using (Scoped.Text(wordWrap: false)) {
			for (var i = 0; i < _longTicks.Count; i++) {
				(int index, long ticks) = _longTicks[i];
				var line = $"#{index}: {TicksToMsString(ticks)}";
				Widgets.Label(new Rect(inner.x, y + i * 18f, inner.width, 18f), line);
			}
		}
	}

	private void DrawLists(Rect rect) {
		var cols = rect.ToFlexbox(["1fr", "1fr"], 10f).ToList();
		DrawAggregateTable(cols[0], "By type (Thing.DoTick)", _typeEntries, _totalTypedTicks, ref _scrollTypes);
		DrawAggregateTable(cols[1], "By key (selected pawns)", _keyEntries, _totalKeyedTicks, ref _scrollKeys);
	}

	private sealed class AggregateEntry(string label) {
		public string Label { get; } = label;

		public int Count { get; private set; }

		public long Ticks { get; private set; }

		public double AverageTicks => Count == 0 ? 0 : (double)Ticks / Count;

		public void Add(ProfilerRecord record) {
			Count += record.Count;
			Ticks += record.Ticks;
		}
	}
}