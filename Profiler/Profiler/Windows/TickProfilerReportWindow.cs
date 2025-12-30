using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TrueMogician.RimWorld.Profiler.Patches;
using TrueMogician.RimWorld.Utility.GUI;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;
using static TrueMogician.RimWorld.Utility.Formatter;

namespace TrueMogician.RimWorld.Profiler.Windows;

public sealed class TickProfilerReportWindow : Window {
	private const float _ROW_HEIGHT = 24f;
	private const float _SCROLLBAR_WIDTH = 16f;
	private const float _CLOSE_BUTTON_RESERVE_HEIGHT = 45f;
	private const float _SECTION_GAP = 8f;
	private const float _SECTION_PADDING = 8f;
	private const float _TABLE_ROW_GAP = 4f;
	private const float _TABLE_COLUMN_GAP = 6f;

	private static readonly Flexbox.Length[] _TABLE_COLUMNS = [80f, 35f, 60f, 80f, Flexbox.Length.Auto];
	private readonly List<AggregateEntry> _keyEntries;
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
			_typeEntries = [];
			_keyEntries = [];
		}
		else {
			_totalTickTicks = _records.Sum(r => r.Time);
			_minTickTicks = _records.Min(r => r.Time);
			_maxTickTicks = _records.Max(r => r.Time);

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

	public override Vector2 InitialSize => new(1200f, 750f);

	public override void DoWindowContents(Rect inRect) {
		var content = inRect;
		content.height = Mathf.Max(0f, content.height - _CLOSE_BUTTON_RESERVE_HEIGHT);
		var rows = content.ToFlexbox(FlexDirection.Column, [30f, 60f, "1fr"], _SECTION_GAP).ToList();
		using (Scoped.Text(TextAnchor.MiddleCenter, GameFont.Medium))
			Widgets.Label(rows[0], "Tick Profiler Report");
		using (Scoped.Text(font: GameFont.Small)) {
			DrawSummary(rows[1]);
			DrawLists(rows[2]);
		}
	}

	private static void DrawAggregateTable(Rect rect, string title, List<AggregateEntry> entries, long totalTicks, ref Vector2 scrollPos) {
		Widgets.DrawMenuSection(rect);
		var inner = rect.ContractedBy(_SECTION_PADDING);

		var rows = inner.ToFlexbox(FlexDirection.Column, [22f, 22f, "1fr"], _TABLE_ROW_GAP).ToList();
		Widgets.Label(rows[0], $"{title} — {Bold(entries.Count)} entries");
		DrawHeaderRow(rows[1]);
		var outRect = rows[2];
		float viewHeight = entries.Count * _ROW_HEIGHT;
		var widths = outRect.ToFlexbox(["1fr", _SCROLLBAR_WIDTH]).ToList();
		var viewRect = new Rect(0f, 0f, widths[0].width, Math.Max(outRect.height, viewHeight));

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
		var cols = rect.ToFlexbox(_TABLE_COLUMNS, _TABLE_COLUMN_GAP).ToList();
		using (Scoped.Text(TextAnchor.MiddleRight)) {
			Widgets.Label(cols[0], Bold("Time"));
			Widgets.Label(cols[1], Bold("%"));
			Widgets.Label(cols[2], Bold("Count"));
			Widgets.Label(cols[3], Bold("Avg"));
		}
		using (Scoped.Text(TextAnchor.MiddleLeft))
			Widgets.Label(cols[4], Bold("Label"));
	}

	private static void DrawEntryRow(Rect rect, AggregateEntry entry, long totalTicks) {
		var cols = rect.ToFlexbox(_TABLE_COLUMNS, _TABLE_COLUMN_GAP).ToList();

		double pct = totalTicks == 0 ? 0 : entry.Ticks * 100.0 / totalTicks;
		double avg = entry.Count == 0 ? 0 : entry.AverageTicks * 1_000_000.0 / Stopwatch.Frequency;

		using (Scoped.Text(TextAnchor.MiddleRight)) {
			Widgets.Label(cols[0], FormatTicks(entry.Ticks));
			Widgets.Label(cols[1], $"{pct:F1}");
			Widgets.Label(cols[2], FormatCount(entry.Count));
			Widgets.Label(cols[3], FormatTime(avg, "µs"));
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

	private static string FormatTime(double time, string unit = "ms") => Colored(time.ToString("F2"), Color.green) + $" {unit}";

	private static string FormatTicks(long ticks) => FormatTime(TicksToMs(ticks));

	private static string FormatCount(int count) => Colored(count, Color.cyan);

	private static void DrawSummaryRow(Rect rect, params (string Label, string Value)?[] items) {
		var flexbox = rect.ToFlexbox(
			FlexDirection.Row,
			Enumerable.Repeat<Flexbox.Length>(200f, items.Length),
			justifyContent: JustifyContent.SpaceBetween
		);
		foreach (var (item, group) in items.Zip(flexbox, (i, g) => (i, g))) {
			if (item is null)
				continue;
			(string label, string value) = item.Value;
			var cols = group.ToFlexbox([55f, Flexbox.Length.Auto], 4f).ToList();
			using (Scoped.Text(TextAnchor.MiddleLeft)) {
				Widgets.Label(cols[0], Bold(label + ":"));
				Widgets.Label(cols[1], value);
			}
		}
	}

	private void DrawSummary(Rect rect) {
		Widgets.DrawMenuSection(rect);
		var inner = rect.ContractedBy(_SECTION_PADDING);

		int tickCount = _records.Count;
		double avgTickMs = tickCount == 0 ? 0 : TicksToMs(_totalTickTicks) / tickCount;
		var rows = inner.ToFlexbox(FlexDirection.Column, 2, 2f).ToList();
		var typedPct = _totalTickTicks == 0 ? 0 : _totalTypedTicks * 100.0 / _totalTickTicks;
		var keyedPct = _totalTickTicks == 0 ? 0 : _totalKeyedTicks * 100.0 / _totalTickTicks;
		using (Scoped.Text(TextAnchor.UpperLeft, wordWrap: false)) {
			DrawSummaryRow(
				rows[0],
				("Ticks", FormatCount(tickCount)),
				("Total", FormatTicks(_totalTickTicks)),
				("Typed", FormatTicks(_totalTypedTicks) + $" ({typedPct:F1}%)"),
				("Keyed", FormatTicks(_totalKeyedTicks) + $" ({keyedPct:F1}%)")
			);
			DrawSummaryRow(
				rows[1],
				("Avg", FormatTime(avgTickMs)),
				("Min", FormatTicks(_minTickTicks)),
				("Max", FormatTicks(_maxTickTicks)),
				null
			);
		}
	}

	private void DrawLists(Rect rect) {
		var cols = rect.ToFlexbox(FlexDirection.Row, 2, _SECTION_GAP).ToList();
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