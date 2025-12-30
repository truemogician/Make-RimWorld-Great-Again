using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using TrueMogician.Extensions.List;
using TrueMogician.RimWorld.Profiler.Patches;
using TrueMogician.RimWorld.Utility.GUI;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;
using static TrueMogician.RimWorld.Utility.Formatter;

// ReSharper disable ChangeFieldTypeToSystemThreadingLock

namespace TrueMogician.RimWorld.Profiler.Windows;

using static Helper;

public sealed class TickProfilerReportWindow : Window {
	private const float _ROW_HEIGHT = 24f;
	private const float _SCROLLBAR_WIDTH = 16f;
	private const float _SECTION_GAP = 8f;
	private const float _SECTION_PADDING = 8f;
	private const float _TABLE_ROW_GAP = 4f;
	private const float _TABLE_COLUMN_GAP = 6f;
	private const float _BUTTONS_HEIGHT = 38f;
	private const float _LIST_UPDATE_FREQUENCY = 2f;

	private static readonly Flexbox.Length[] _TABLE_COLUMNS = [80f, 35f, 60f, 80f, Flexbox.Length.Auto];

	private readonly object _aggregateLock = new();
	private int _lastAllRecordsCount;
	private float _nextListsUpdateAt;
	private bool _updatingLists;
	private Thread? _refreshThread;

	private readonly EntryList _typedList = new();
	private readonly EntryList _keyedList = new();

	public TickProfilerReportWindow() {
		doCloseX = true;
		doCloseButton = false;
		draggable = true;
		absorbInputAroundWindow = true;
	}

	public override Vector2 InitialSize => new(1500f, 750f);

	public override void DoWindowContents(Rect inRect) {
		DoManagerContents(inRect, true);
	}

	public void DoManagerContents(Rect inRect, bool showTitle) {
		UpdateLists(false);
		List<Flexbox.Length> heights = [60f, Flexbox.Length.Auto, _BUTTONS_HEIGHT];
		if (showTitle)
			heights.Insert(0, 30f);
		var rows = inRect.ToFlexbox(FlexDirection.Column, heights, _SECTION_GAP).ToList();
		var rowIndex = 0;

		if (showTitle) {
			using (Scoped.Text(TextAnchor.MiddleCenter, GameFont.Medium))
				Widgets.Label(rows[rowIndex], "Tick Profiler");
			rowIndex++;
		}

		using (Scoped.Text(font: GameFont.Small)) {
			DrawSummary(rows[rowIndex]);
			rowIndex++;
			DrawLists(rows[rowIndex]);
			rowIndex++;
			DrawButtons(rows[rowIndex]);
		}
	}

	private static void DrawAggregateTable(Rect rect, string title, EntryList list) {
		Widgets.DrawMenuSection(rect);
		var inner = rect.ContractedBy(_SECTION_PADDING);

		var rows = inner.ToFlexbox(FlexDirection.Column, [22f, 22f, Flexbox.Length.Auto], _TABLE_ROW_GAP).ToList();
		Widgets.Label(rows[0], $"{title} — {Bold(list.Entries.Count)} entries");
		DrawHeaderRow(rows[1]);
		var outRect = rows[2];
		float viewHeight = list.Entries.Count * _ROW_HEIGHT;
		var widths = outRect.ToFlexbox([Flexbox.Length.Auto, _SCROLLBAR_WIDTH]).ToList();
		var viewRect = new Rect(0f, 0f, widths[0].width, Math.Max(outRect.height, viewHeight));

		Widgets.BeginScrollView(outRect, ref list.ScrollPos, viewRect);
		try {
			Text.WordWrap = false;
			for (var i = 0; i < list.Entries.Count; i++) {
				var rowRect = new Rect(0f, i * _ROW_HEIGHT, viewRect.width, _ROW_HEIGHT);
				if (Mouse.IsOver(rowRect))
					Widgets.DrawHighlight(rowRect);
				DrawEntryRow(rowRect, list.Entries[i], list.TotalTime);
			}
		}
		finally {
			Text.WordWrap = true;
			Widgets.EndScrollView();
		}
	}

	private static void DrawEntryRow(Rect rect, AggregateEntry entry, long totalTicks) {
		var cols = rect.ToFlexbox(_TABLE_COLUMNS, _TABLE_COLUMN_GAP).ToList();

		double pct = totalTicks == 0 ? 0 : entry.Ticks * 100.0 / totalTicks;
		double avg = entry.Count == 0 ? 0 : entry.AverageTicks * 1_000_000.0 / Stopwatch.Frequency;

		using (Scoped.Text(TextAnchor.MiddleRight)) {
			Widgets.Label(cols[0], FormatTimeInTick(entry.Ticks));
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

	private static string FormatCount(int count) => Colored(count, Color.cyan);

	private static string FormatTime(double time, string unit = "ms") => Colored(time.ToString("F2"), Color.green) + $" {unit}";

	private static string FormatTimeInTick(long ticks) => FormatTime(TickToMs(ticks));

	private static string FormatTimeInTick(double ticks) => FormatTime(TickToMs(ticks));

	private void DrawSummary(Rect rect) {
		Widgets.DrawMenuSection(rect);
		var inner = rect.ContractedBy(_SECTION_PADDING);
		var rows = inner.ToFlexbox(FlexDirection.Column, 2, 2f).ToList();
		var summary = TickPatches.Summary;
		using (Scoped.Text(TextAnchor.UpperLeft, wordWrap: false)) {
			DrawSummaryRow(
				rows[0],
				("Ticks", FormatCount(summary.TickCount)),
				("Total", FormatTimeInTick(summary.TotalTime)),
				("Typed", FormatTimeInTick(summary.TotalTypedTime) + $" ({summary.TypedPercent:F1}%)"),
				("Keyed", FormatTimeInTick(summary.TotalKeyedTime) + $" ({summary.KeyedPercent:F1}%)")
			);
			DrawSummaryRow(
				rows[1],
				("Avg", FormatTimeInTick(summary.AvgTime)),
				("Min", FormatTimeInTick(summary.MinTime)),
				("Max", FormatTimeInTick(summary.MaxTime)),
				null
			);
		}
	}

	private void DrawLists(Rect rect) {
		var cols = rect.ToFlexbox(FlexDirection.Row, 2, _SECTION_GAP).ToList();
		lock (_aggregateLock) {
			DrawAggregateTable(cols[0], "By type (Thing.DoTick)", _typedList);
			DrawAggregateTable(cols[1], "By key (selected pawns)", _keyedList);
		}
	}

	private void DrawButtons(Rect rect) {
		var cols = rect.ToFlexbox(
				FlexDirection.Row,
				Enumerable.Repeat<Flexbox.Length>(200f, 2),
				10f,
				JustifyContent.Center
			)
			.ToList();

		if (TickPatches.Enabled) {
			if (Widgets.ButtonText(cols[0], "Stop"))
				TickPatches.Stop();
		}
		else {
			if (Widgets.ButtonText(cols[0], "Start"))
				TickPatches.Start();
		}
		if (Widgets.ButtonText(cols[1], "Reset")) {
			TickPatches.Reset();
			lock (_aggregateLock) {
				_typedList.Entries = [];
				_keyedList.Entries = [];
			}
			_lastAllRecordsCount = 0;
		}
	}

	private void Aggregate(IReadOnlyCollection<SingleTickRecord> records) {
		if (records.Count == 0)
			return;
		_updatingLists = true;
		try {
			Dictionary<string, AggregateEntry> typeEntries, keyEntries;
			long totalTypedTime, totalKeyedTime;
			lock (_aggregateLock) {
				typeEntries = _typedList.Entries.ToDictionary(e => e.Label, e => e.Clone());
				keyEntries = _keyedList.Entries.ToDictionary(e => e.Label, e => e.Clone());
				totalTypedTime = TickPatches.Summary.TotalTypedTime;
				totalKeyedTime = TickPatches.Summary.TotalKeyedTime;
			}

			foreach (var tick in records) {
				foreach (var kvp in tick.TypedRecords) {
					string label = kvp.Value.Label;
					if (!typeEntries.TryGetValue(label, out var agg))
						typeEntries[label] = agg = new AggregateEntry(label);
					agg.Add(kvp.Value);
				}
				foreach (var kvp in tick.KeyedRecords) {
					string label = kvp.Value.Label;
					if (!keyEntries.TryGetValue(label, out var agg))
						keyEntries[label] = agg = new AggregateEntry(label);
					agg.Add(kvp.Value);
				}
			}
			var newTypes = typeEntries.Values.ToList();
			var newKeys = keyEntries.Values.ToList();
			newTypes.Sort(AggregateEntry.TicksComparer);
			newKeys.Sort(AggregateEntry.TicksComparer);

			lock (_aggregateLock) {
				_typedList.Entries = newTypes;
				_keyedList.Entries = newKeys;
				_typedList.TotalTime = totalTypedTime;
				_keyedList.TotalTime = totalKeyedTime;
			}
		}
		finally {
			_updatingLists = false;
		}
	}

	private void UpdateLists(bool sync) {
		if (_updatingLists || Time.realtimeSinceStartup < _nextListsUpdateAt)
			return;

		var newRecords = TickPatches.AllRecords.Slice(_lastAllRecordsCount);
		_lastAllRecordsCount += newRecords.Count;
		_nextListsUpdateAt = Time.realtimeSinceStartup + 1f / _LIST_UPDATE_FREQUENCY;

		if (sync) {
			Aggregate(newRecords);
			return;
		}
		_refreshThread = new Thread(() => Aggregate(newRecords)) {
			IsBackground = true,
			Name = "TickProfilerRefresh"
		};
		_refreshThread.Start();
	}

	internal sealed class EntryList {
		public Vector2 ScrollPos;

		public long TotalTime;

		public List<AggregateEntry> Entries = [];
	}

	internal sealed class AggregateEntry(string label) {
		public static IComparer<AggregateEntry> CountComparer { get; }
			= Comparer<AggregateEntry>.Create((a, b) => b.Count.CompareTo(a.Count));

		public static IComparer<AggregateEntry> TicksComparer { get; }
			= Comparer<AggregateEntry>.Create((a, b) => b.Ticks.CompareTo(a.Ticks));

		public string Label { get; } = label;

		public int Count { get; private set; }

		public long Ticks { get; private set; }

		public double AverageTicks => Count == 0 ? 0 : (double)Ticks / Count;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(ProfilerRecord record) {
			Count += record.Count;
			Ticks += record.Ticks;
		}

		public AggregateEntry Clone() => new(Label) { Count = Count, Ticks = Ticks };
	}
}