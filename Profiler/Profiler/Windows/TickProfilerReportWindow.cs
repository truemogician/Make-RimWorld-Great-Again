using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using TrueMogician.RimWorld.Profiler.Patches;
using TrueMogician.RimWorld.Utility.Extensions;
using TrueMogician.RimWorld.Utility.GUI;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;
using static TrueMogician.RimWorld.Utility.Formatter;

// ReSharper disable ChangeFieldTypeToSystemThreadingLock

namespace TrueMogician.RimWorld.Profiler.Windows;

using static Helper;

public sealed class TickProfilerReportWindow : Window {
	private const float _SCROLLBAR_WIDTH = 16f;
	private const float _SIDEBAR_WIDTH = 160f;
	private const float _SECTION_GAP = 8f;
	private const float _SUMMARY_HEIGHT = 60f;
	private const float _SECTION_PADDING = 12f;
	private const float _TABLE_ROW_HEIGHT = 30f;
	private const float _TABLE_COLUMN_GAP = 8f;
	private const int _TABLE_BORDER_THICKNESS = 1;
	private const float _BUTTONS_HEIGHT = 36f;
	private const float _LIST_UPDATE_FREQUENCY = 2f;

	private static readonly Flexbox.Length[] _TABLE_COLUMNS = [Flexbox.Length.Auto, 40f, 80f, 80f, 80f, 80f, 80f];
	private static readonly string[] _TABLE_COLUMN_LABELS = ["Label", "%", "Count", "Time", "Avg", "Max", "MAD"];
	private static readonly Color _ROW_BORDER_COLOR = new(0.75f, 0.75f, 0.75f, 0.9f);
	private static readonly Color[] _PROPORTION_BAR_COLORS = [
		new(0.75f, 0.15f, 0.12f, 0.5f),
		new(0.85f, 0.75f, 0.18f, 0.5f),
		new(0.55f, 0.7f, 0.85f, 0.5f),
		new(0.6f, 0.6f, 0.6f, 0.5f)
	];

	private readonly object _aggregateLock = new();
	private readonly List<TabRecord> _tabs = [];
	private int _lastAllRecordsCount;
	private float _nextListsUpdateAt;
	private bool _updatingLists;
	private ReportTab _selectedTab;

	private readonly EntryList _typedList = new();
	private readonly EntryList _pawnList = new();
	private readonly EntryList _keyedList = new();

	public TickProfilerReportWindow() {
		doCloseX = true;
		doCloseButton = false;
		draggable = true;
		absorbInputAroundWindow = true;
		_tabs.Add(new TabRecord("Types", () => _selectedTab = ReportTab.Types, () => _selectedTab == ReportTab.Types));
		_tabs.Add(new TabRecord("Pawn categories", () => _selectedTab = ReportTab.Pawns, () => _selectedTab == ReportTab.Pawns));
		_tabs.Add(new TabRecord("Selected pawns", () => _selectedTab = ReportTab.Keys, () => _selectedTab == ReportTab.Keys));
	}

	private enum ReportTab : byte {
		Types,
		Pawns,
		Keys
	}

	public override Vector2 InitialSize => new(1500f, 750f);

	public override void DoWindowContents(Rect inRect) => DoManagerContents(inRect, true);

	public void DoManagerContents(Rect inRect, bool showTitle) {
		UpdateLists(false);
		List<Flexbox.Length> heights = [_SUMMARY_HEIGHT, Flexbox.Length.Auto, _BUTTONS_HEIGHT];
		if (showTitle)
			heights.Insert(0, 30f);
		var rows = inRect.ToFlexbox(FlexDirection.Column, heights, _SECTION_GAP).ToList();
		var idx = 0;

		if (showTitle) {
			using (new TextBlock(GameFont.Medium, TextAnchor.MiddleCenter))
				Widgets.Label(rows[idx], "Tick Profiler");
			idx++;
		}

		using (new TextBlock(GameFont.Small)) {
			DrawSummary(rows[idx]);
			idx++;
			DrawLists(rows[idx]);
			idx++;
			DrawButtons(rows[idx]);
		}
	}

	private static void DrawAggregateTable(Rect rect, EntryList list) {
		Widgets.DrawMenuSection(rect);
		var inner = rect.Padding(_SECTION_PADDING);
		var sections = inner.ToFlexbox(FlexDirection.Column, [_TABLE_ROW_HEIGHT, Flexbox.Length.Auto]).ToArray();
		float viewHeight = list.Entries.Count * _TABLE_ROW_HEIGHT;
		var viewRect = new Rect(0f, 0f, sections[1].width, Math.Max(sections[1].height, viewHeight));
		if ((list.Entries.Count + 1) * _TABLE_ROW_HEIGHT >= inner.height) {
			sections[0].width -= _SCROLLBAR_WIDTH;
			viewRect.width -= _SCROLLBAR_WIDTH;
		}
		DrawHeaderRow(sections[0]);
		Widgets.BeginScrollView(sections[1], ref list.ScrollPos, viewRect);
		try {
			Text.WordWrap = false;
			for (var i = 0; i < list.Entries.Count; i++) {
				var rowRect = new Rect(0f, _TABLE_ROW_HEIGHT * i, viewRect.width, _TABLE_ROW_HEIGHT);
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

	private static void DrawHeaderRow(Rect rect) {
		DrawRowBorder(rect);
		var cols = rect.Padding(0, _TABLE_COLUMN_GAP).ToFlexbox(_TABLE_COLUMNS, _TABLE_COLUMN_GAP).ToList();
		using (new TextBlock(TextAnchor.MiddleLeft))
			Widgets.Label(cols[0], Bold(_TABLE_COLUMN_LABELS[0]));
		using (new TextBlock(TextAnchor.MiddleRight)) {
			for (var i = 1; i < _TABLE_COLUMN_LABELS.Length; ++i)
				Widgets.Label(cols[i], Bold(_TABLE_COLUMN_LABELS[i]));
		}
	}

	private static void DrawEntryRow(Rect rect, AggregateEntry entry, long totalTicks) {
		var cols = rect.Padding(0, _TABLE_COLUMN_GAP).ToFlexbox(_TABLE_COLUMNS, _TABLE_COLUMN_GAP).ToList();
		double pct = totalTicks == 0 ? 0 : entry.Ticks / (double)totalTicks;
		double avg = entry.Count == 0 ? 0 : entry.AverageTicks * 1_000_000.0 / Stopwatch.Frequency;
		var barRect = rect.Padding(0, _TABLE_BORDER_THICKNESS, _TABLE_BORDER_THICKNESS);
		barRect.width *= Mathf.Clamp01((float)pct);
		Widgets.DrawBoxSolid(barRect, ProportionBarColor(pct));
		using (new TextBlock(TextAnchor.MiddleLeft)) {
			var labelRect = cols[0];
			string truncated = entry.Label.Truncate(labelRect.width);
			Widgets.Label(labelRect, truncated);
			if (Mouse.IsOver(labelRect))
				TooltipHandler.TipRegion(labelRect, entry.Label);
		}
		using (new TextBlock(TextAnchor.MiddleRight)) {
			Widgets.Label(cols[1], $"{pct * 100.0:F1}");
			Widgets.Label(cols[2], FormatCount(entry.Count));
			Widgets.Label(cols[3], FormatTimeInTick(entry.Ticks));
			Widgets.Label(cols[4], FormatTime(avg, "µs"));
			Widgets.Label(cols[5], FormatTickDuration(entry.MaxTicks));
			Widgets.Label(cols[6], FormatTickDuration(entry.MAD));
		}
		DrawRowBorder(rect, BorderEdges.Bottom | BorderEdges.Left | BorderEdges.Right);
	}

	private static void DrawRowBorder(Rect rect, BorderEdges edges = BorderEdges.All) {
		using (new TextBlock(_ROW_BORDER_COLOR))
			WidgetsExtension.DrawBorder(rect, edges);
	}

	private static Color ProportionBarColor(double pct) => pct switch {
		>= 0.20 => _PROPORTION_BAR_COLORS[0],
		>= 0.05 => _PROPORTION_BAR_COLORS[1],
		>= 0.01 => _PROPORTION_BAR_COLORS[2],
		_       => _PROPORTION_BAR_COLORS[3]
	};

	private static void DrawSummaryRow(Rect rect, params (string Label, string Value)[] items) {
		var flexbox = rect.ToFlexbox(
			FlexDirection.Row,
			items.Length,
			justifyContent: JustifyContent.SpaceBetween
		);
		foreach (var (item, group) in items.Zip(flexbox, (i, g) => (i, g))) {
			(string label, string value) = item;
			var cols = group.ToFlexbox([55f, Flexbox.Length.Auto], 4f).ToList();
			using (new TextBlock(TextAnchor.MiddleLeft)) {
				Widgets.Label(cols[0], Bold(label + ":"));
				Widgets.Label(cols[1], value);
			}
		}
	}

	private static string FormatCount(int count) => Colored(count, Color.cyan);

	private static string FormatTime(double time, string unit = "ms") => Colored(time.ToString("F2"), Color.green) + $" {unit}";

	private static string FormatTimeInTick(long ticks) => FormatTime(TickToMs(ticks));

	private static string FormatTimeInTick(double ticks) => FormatTime(TickToMs(ticks));

	private static string FormatTickDuration(double ticks) {
		double ms = TickToMs(ticks);
		return ms < 1 ? FormatTime(ms * 1000.0, "µs") : FormatTime(ms);
	}

	private static string FormatTimeWithPercent(long ticks, long totalTicks) {
		double pct = totalTicks == 0 ? 0 : ticks * 100.0 / totalTicks;
		return FormatTimeInTick(ticks) + $" ({pct:F1}%)";
	}

	private static string FormatPawnLabel(PawnProps props) {
		if (!props.OnActiveMap)
			return "Off-map pawns";
		var label = props.State != PawnState.Active ? props.State.ToString() : props.Type.ToString();
		return GenText.SplitCamelCase(label).CapitalizeFirst();
	}

	private static void AddAggregateEntry(Dictionary<string, AggregateEntry> entries, string label, int count, long qpcTicks) {
		if (!entries.TryGetValue(label, out var agg))
			entries[label] = agg = new AggregateEntry(label);
		agg.Add(count, qpcTicks);
	}

	private void DrawSummary(Rect rect) {
		Widgets.DrawMenuSection(rect);
		var inner = rect.Padding(_SECTION_PADDING / 2, _SECTION_PADDING);
		var rows = inner.ToFlexbox(FlexDirection.Column, 2, 2f).ToList();
		var summary = TickPatches.Summary;
		long typedTime, pawnTime, keyedTime;
		lock (_aggregateLock) {
			typedTime = _typedList.TotalTime;
			pawnTime = _pawnList.TotalTime;
			keyedTime = _keyedList.TotalTime;
		}
		using (new TextBlock(null, TextAnchor.UpperLeft, false)) {
			DrawSummaryRow(
				rows[0],
				("Ticks", FormatCount(summary.TickCount)),
				("Total", FormatTimeInTick(summary.TotalTime)),
				("Avg", FormatTimeInTick(summary.AvgTime)),
				("Min", FormatTimeInTick(summary.MinTime)),
				("Max", FormatTimeInTick(summary.MaxTime))
			);
			DrawSummaryRow(
				rows[1],
				("Things", FormatTimeWithPercent(typedTime, summary.TotalTime)),
				("Pawns", FormatTimeWithPercent(pawnTime, summary.TotalTime)),
				("Keyed", FormatTimeWithPercent(keyedTime, summary.TotalTime))
			);
		}
	}

	private void DrawLists(Rect rect) {
		var cols = rect.ToFlexbox([_SIDEBAR_WIDTH, Flexbox.Length.Auto], _SECTION_GAP).ToList();
		DrawTabSidebar(cols[0]);
		lock (_aggregateLock) {
			switch (_selectedTab) {
				case ReportTab.Types: DrawAggregateTable(cols[1], _typedList); break;
				case ReportTab.Pawns: DrawAggregateTable(cols[1], _pawnList); break;
				case ReportTab.Keys:  DrawAggregateTable(cols[1], _keyedList); break;
				default:              throw new ArgumentOutOfRangeException();
			}
		}
	}

	private void DrawTabSidebar(Rect rect) {
		Widgets.DrawMenuSection(rect);
		var inner = rect.Padding(_SECTION_PADDING);
		var rows = inner.ToFlexbox(
				FlexDirection.Column,
				Enumerable.Repeat<Flexbox.Length>(_TABLE_ROW_HEIGHT, _tabs.Count),
				_TABLE_COLUMN_GAP / 2
			)
			.ToArray();
		for (var i = 0; i < _tabs.Count; ++i) {
			var tab = _tabs[i];
			if (tab.Selected)
				Widgets.DrawHighlightSelected(rows[i]);
			else if (Mouse.IsOver(rows[i]))
				Widgets.DrawHighlight(rows[i]);
			using (new TextBlock(TextAnchor.MiddleLeft))
				Widgets.Label(rows[i].Padding(0f, _TABLE_COLUMN_GAP), tab.label);
			if (Widgets.ButtonInvisible(rows[i]))
				tab.clickedAction?.Invoke();
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
				_pawnList.Entries = [];
				_keyedList.Entries = [];
				_typedList.TotalTime = 0L;
				_pawnList.TotalTime = 0L;
				_keyedList.TotalTime = 0L;
			}
			lock (TickPatches.RecordsLock)
				_lastAllRecordsCount = 0;
		}
	}

	private void Aggregate(IReadOnlyCollection<SingleTickRecord> records) {
		if (records.Count == 0)
			return;
		_updatingLists = true;
		try {
			Dictionary<string, AggregateEntry> typeEntries, pawnEntries, keyEntries;
			lock (_aggregateLock) {
				typeEntries = _typedList.Entries.ToDictionary(e => e.Label, e => e.Clone());
				pawnEntries = _pawnList.Entries.ToDictionary(e => e.Label, e => e.Clone());
				keyEntries = _keyedList.Entries.ToDictionary(e => e.Label, e => e.Clone());
			}

			foreach (var tick in records) {
				foreach (var record in tick.TypeRecord)
					AddAggregateEntry(typeEntries, record.Key.Name, record.HitCount, record.QpcTicks);
				foreach (var record in tick.PawnRecord)
					AddAggregateEntry(pawnEntries, FormatPawnLabel(record.Key), record.HitCount, record.QpcTicks);
				foreach (var record in tick.KeyedRecord)
					AddAggregateEntry(keyEntries, record.Key, record.HitCount, record.QpcTicks);
			}
			var newTypes = typeEntries.Values.ToList();
			var newPawns = pawnEntries.Values.ToList();
			var newKeys = keyEntries.Values.ToList();
			newTypes.Sort(AggregateEntry.TicksComparer);
			newPawns.Sort(AggregateEntry.TicksComparer);
			newKeys.Sort(AggregateEntry.TicksComparer);

			lock (_aggregateLock) {
				_typedList.Entries = newTypes;
				_pawnList.Entries = newPawns;
				_keyedList.Entries = newKeys;
				_typedList.TotalTime = newTypes.Sum(e => e.Ticks);
				_pawnList.TotalTime = newPawns.Sum(e => e.Ticks);
				_keyedList.TotalTime = newKeys.Sum(e => e.Ticks);
			}
		}
		finally {
			_updatingLists = false;
		}
	}

	private void UpdateLists(bool sync) {
		if (_updatingLists || Time.realtimeSinceStartup < _nextListsUpdateAt)
			return;

		List<SingleTickRecord> newRecords;
		lock (TickPatches.RecordsLock) {
			if (_lastAllRecordsCount > TickPatches.AllRecords.Count)
				_lastAllRecordsCount = TickPatches.AllRecords.Count;
			var count = TickPatches.AllRecords.Count - _lastAllRecordsCount;
			if (count == 0)
				return;
			newRecords = TickPatches.AllRecords.GetRange(_lastAllRecordsCount, count);
			_lastAllRecordsCount = TickPatches.AllRecords.Count;
		}
		_nextListsUpdateAt = Time.realtimeSinceStartup + 1f / _LIST_UPDATE_FREQUENCY;

		if (sync) {
			Aggregate(newRecords);
			return;
		}
		var refreshThread = new Thread(() => Aggregate(newRecords)) {
			IsBackground = true,
			Name = "TickProfilerRefresh"
		};
		refreshThread.Start();
	}

	internal sealed class EntryList {
		public Vector2 ScrollPos;

		public long TotalTime;

		public List<AggregateEntry> Entries = [];
	}

	internal sealed class AggregateEntry(string label) {
		private List<long> _samples = [];

		public static IComparer<AggregateEntry> CountComparer { get; }
			= Comparer<AggregateEntry>.Create((a, b) => b.Count.CompareTo(a.Count));

		public static IComparer<AggregateEntry> TicksComparer { get; }
			= Comparer<AggregateEntry>.Create((a, b) => b.Ticks.CompareTo(a.Ticks));

		public string Label { get; } = label;

		public int Count { get; private set; }

		public long Ticks { get; private set; }

		public long MaxTicks { get; private set; }

		public double MAD {
			get {
				if (_samples.Count == 0)
					return 0;
				double median = Median(_samples);
				var deviations = new List<long>(_samples.Count);
				foreach (long sample in _samples)
					deviations.Add((long)Math.Abs(sample - median));
				return Median(deviations);
			}
		}

		public double AverageTicks => Count == 0 ? 0 : (double)Ticks / Count;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(int count, long qpcTicks) {
			Count += count;
			Ticks += qpcTicks;
			_samples.Add(qpcTicks);
			if (qpcTicks > MaxTicks)
				MaxTicks = qpcTicks;
		}

		public AggregateEntry Clone() =>
			new(Label) { Count = Count, Ticks = Ticks, MaxTicks = MaxTicks, _samples = [.._samples] };

		private static double Median(List<long> values) {
			if (values.Count == 0)
				return 0;
			values.Sort();
			int middle = values.Count / 2;
			return values.Count % 2 == 0 ? (values[middle - 1] + values[middle]) / 2.0 : values[middle];
		}
	}
}