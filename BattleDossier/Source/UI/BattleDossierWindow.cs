using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using TrueMogician.RimWorld.BattleDossier.Components;
using TrueMogician.RimWorld.BattleDossier.Core;
using TrueMogician.RimWorld.BattleDossier.Models;
using TrueMogician.RimWorld.BattleDossier.Static;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.BattleDossier.UI;

/// <summary>
///     The dossier viewer: a bottom-bar main tab with a browser sidebar (live sessions on top) and a tabbed
///     detail pane (Overview / Leaderboard / Timeline). The leaderboard and timeline are both derived from the
///     record's log; live references only power the jump-to affordance.
/// </summary>
public class BattleDossierWindow : MainTabWindow {
	private const float _BROWSER_WIDTH = 230f;
	private const float _ROW_HEIGHT = 28f;
	private const float _HEADER_HEIGHT = 34f;
	private const float _TIME_WIDTH = 84f;
	private const float _LOG_TEXT_X = 110f;

	private static readonly Color _inProgressColor = new(1f, 0.85f, 0.4f);
	private static readonly Color _killColor = new(1f, 0.6f, 0.6f);
	private static readonly Color _downColor = new(1f, 0.85f, 0.5f);
	private static readonly Color _captureColor = new(0.6f, 0.8f, 1f);
	private static readonly Color _hitColor = new(0.7f, 0.7f, 0.7f);
	private static readonly Texture2D _pinTex = ContentFinder<Texture2D>.Get("UI/Icons/Pin");
	private static readonly Texture2D _pinOutlineTex = ContentFinder<Texture2D>.Get("UI/Icons/Pin-Outline");

	private static readonly Column[] _columns = [
		new("Name", 0f, (a, b) => string.Compare(a.Info.Label, b.Info.Label, StringComparison.OrdinalIgnoreCase)),
		new("Side", 70f, (a, b) => a.Info.Side.CompareTo(b.Info.Side)),
		new("DamageDealt", 80f, (a, b) => a.Summary.DamageDealt.CompareTo(b.Summary.DamageDealt)),
		new("Kills", 50f, (a, b) => a.Summary.Kills.CompareTo(b.Summary.Kills)),
		new("Downs", 55f, (a, b) => a.Summary.Downs.CompareTo(b.Summary.Downs)),
		new("DamageTaken", 80f, (a, b) => a.Summary.DamageTaken.CompareTo(b.Summary.DamageTaken)),
		new("FriendlyFire", 70f, (a, b) => a.Summary.FriendlyFire.CompareTo(b.Summary.FriendlyFire)),
		new("Fate", 75f, (a, b) => a.Summary.Fate.CompareTo(b.Summary.Fate))
	];

	private BattleDossierRecord? _selected;
	private BattleSession? _selectedSession;
	private Tab _tab = Tab.Overview;
	private Vector2 _browserScroll;
	private Vector2 _contentScroll;
	private Vector2 _detailScroll;
	private string _search = "";
	private string? _factionFilter;
	private ParticipantType? _typeFilter;
	private int _sortColumn = 2;
	private bool _sortDescending = true;
	private int _selectedParticipantId = -1;
	private bool _showNonLethal;

	// Derived-stats cache, keyed by the viewed record + its log count (live sessions recompute as logs arrive).
	private BattleDossierRecord? _statsRecord;
	private int _statsCount = -1;
	private BattleStatsResult _stats = new([], new Dictionary<int, ParticipantInfo>());
	private IReadOnlyDictionary<int, ParticipantInfo> _statsParticipants = new Dictionary<int, ParticipantInfo>();

	private enum Tab : byte {
		Overview,
		Leaderboard,
		Timeline
	}

	public override Vector2 RequestedTabSize => new(1010f, 660f);

	private static DossierManager? Manager => DossierManager.Instance;

	/// <summary>Opens the dossier tab and optionally focuses a specific record.</summary>
	public static void Open(BattleDossierRecord? record = null) {
		Find.MainTabsRoot.SetCurrentTab(BattleDossierDefOf.BattleDossier);
		if (record != null && BattleDossierDefOf.BattleDossier.TabWindow is BattleDossierWindow window)
			window.SelectRecord(record);
	}

	public void SelectRecord(BattleDossierRecord record) {
		_selected = record;
		_selectedSession = null;
		ResetFilters();
	}

	public override void PreOpen() {
		base.PreOpen();
		EnsureSelection();
	}

	public override void DoWindowContents(Rect inRect) {
		// A watched live session may have ended; fall back to its finished record.
		if (_selectedSession != null && Manager?.ActiveSessions.Contains(_selectedSession) != true) {
			_selected = _selectedSession.Record;
			_selectedSession = null;
		}
		var browserRect = inRect.LeftPartPixels(_BROWSER_WIDTH);
		var detailRect = new Rect(browserRect.xMax + 12f, inRect.y, inRect.width - _BROWSER_WIDTH - 12f, inRect.height);
		DoBrowser(browserRect);
		Widgets.DrawLineVertical(browserRect.xMax + 6f, inRect.y, inRect.height);
		if (_selectedSession != null)
			DoDetail(detailRect, _selectedSession.Record, true);
		else if (_selected != null)
			DoDetail(detailRect, _selected, false);
		else
			DoEmptyState(detailRect);
	}

	private static string DisplayName(BattleDossierRecord record) {
		if (record.Name.NullOrEmpty())
			return "BattleDossier.UnknownBattle".Translate();
		return record.CustomName ? record.Name : record.Name.StripTags();
	}

	private static string RowTooltip(BattleDossierRecord record) {
		string period = (Find.TickManager.TicksGame - record.EndTick).ToStringTicksToPeriod(false);
		return $"{DisplayName(record)}\n{"BattleDossier.Browser.Tooltip".Translate(record.Participants.Count, period)}";
	}

	private static void DrawCell(ref float x, Rect rowRect, float width, string text) {
		Widgets.Label(new Rect(x, rowRect.y, width, rowRect.height), text);
		x += width;
	}

	private static void DoEmptyState(Rect rect) {
		Text.Anchor = TextAnchor.MiddleCenter;
		GUI.color = Color.gray;
		Widgets.Label(rect, "BattleDossier.NoSelection".Translate());
		GUI.color = Color.white;
		Text.Anchor = TextAnchor.UpperLeft;
	}

	private static (Texture2D? icon, Color color) Style(DossierLog record) => record switch {
		CasualtyLog { Type: CasualtyType.Killed or CasualtyType.Destroyed } => (LogEntry.Skull, _killColor),
		CasualtyLog { Type: CasualtyType.Downed }                           => (LogEntry.Downed, _downColor),
		CasualtyLog { Type: CasualtyType.Captured }                         => (LogEntry.Downed, _captureColor),
		CasualtyLog { Type: CasualtyType.Fled }                             => (null, Color.gray),
		HitLog                                                              => (null, _hitColor),
		_                                                                   => (null, Color.white)
	};

	private static string NameWithKind(ParticipantInfo info) =>
		!info.KindLabel.NullOrEmpty() && info.KindLabel != info.Label ? $"{info.Label} ({info.KindLabel})" : info.Label;

	private static bool DoLeaderboardRow(Rect rect, in Row row, float nameWidth, bool selected) {
		if (selected)
			Widgets.DrawHighlightSelected(rect);
		else if (Mouse.IsOver(rect))
			Widgets.DrawHighlight(rect);
		Text.Anchor = TextAnchor.MiddleLeft;
		float x = rect.x;

		var nameRect = new Rect(x, rect.y, nameWidth, rect.height);
		Widgets.Label(nameRect, NameWithKind(row.Info).Truncate(nameWidth - 30f));
		x += nameWidth;

		// The live pawn/building, or its corpse for a died pawn — whichever still exists on a map.
		var locate = row.Info.LiveThing ?? row.Info.LiveCorpse;
		var jumped = false;
		if (locate is { SpawnedOrAnyParentSpawned: true } target) {
			var jumpRect = new Rect(nameRect.xMax - 26f, rect.y + 2f, 24f, 24f);
			if (Widgets.ButtonImage(jumpRect, TexButton.ShowZones)) {
				CameraJumper.TryJumpAndSelect(target);
				Find.MainTabsRoot.EscapeCurrentTab(false);
				jumped = true;
			}
			TooltipHandler.TipRegionByKey(
				jumpRect,
				row.Info.LiveThing != null ? "BattleDossier.Detail.JumpTooltip" : "BattleDossier.Detail.CorpseTooltip"
			);
		}

		DrawCell(ref x, rect, _columns[1].Width, $"BattleDossier.Side.{row.Info.Side}".Translate());
		DrawCell(ref x, rect, _columns[2].Width, row.Summary.DamageDealt.ToString("F0"));
		DrawCell(ref x, rect, _columns[3].Width, row.Summary.Kills.ToString());
		DrawCell(ref x, rect, _columns[4].Width, row.Summary.Downs.ToString());
		DrawCell(ref x, rect, _columns[5].Width, row.Summary.DamageTaken.ToString("F0"));
		DrawCell(ref x, rect, _columns[6].Width, row.Summary.FriendlyFire.ToString("F0"));
		DrawCell(ref x, rect, _columns[7].Width, $"BattleDossier.Fate.{row.Summary.Fate}".Translate());
		Text.Anchor = TextAnchor.UpperLeft;
		return !jumped && Widgets.ButtonInvisible(rect);
	}

	// #region Stats cache
	private void EnsureStats(BattleDossierRecord record, BattleSession? session) {
		if (_statsRecord == record && _statsCount == record.Logs.Count)
			return;
		_statsRecord = record;
		_statsCount = record.Logs.Count;
		_statsParticipants = session != null ? session.Participants : record.Participants.ToDictionary(p => p.ThingId);
		_stats = new BattleStatsResult(record.Logs, _statsParticipants);
	}
	// #endregion

	/// <summary>Default the selection to the ongoing battle, or the most recent dossier.</summary>
	private void EnsureSelection() {
		var manager = Manager;
		if (manager == null)
			return;
		if (_selectedSession != null && manager.ActiveSessions.Contains(_selectedSession))
			return;
		if (_selected != null && manager.Records.Contains(_selected)) {
			_selectedSession = null;
			return;
		}
		_selectedSession = manager.ActiveSessions.Count > 0 ? manager.ActiveSessions[^1] : null;
		_selected = _selectedSession?.Record ?? (manager.Records.Count > 0 ? manager.Records[^1] : null);
	}

	// #region Browser
	private void DoBrowser(Rect rect) {
		var listing = rect;
		Text.Font = GameFont.Medium;
		Widgets.Label(listing.TopPartPixels(30f), "BattleDossier.Browser.Title".Translate());
		Text.Font = GameFont.Small;
		listing.yMin += 34f;
		_search = Widgets.TextField(listing.TopPartPixels(26f), _search);
		listing.yMin += 30f;

		var manager = Manager;
		if (manager == null)
			return;
		var sessions = manager.ActiveSessions;
		var records = Filtered(manager.Records);
		float viewHeight = (sessions.Count + records.Count) * _ROW_HEIGHT;
		var viewRect = new Rect(0f, 0f, listing.width - 16f, viewHeight);
		Widgets.BeginScrollView(listing, ref _browserScroll, viewRect);
		var y = 0f;
		foreach (var session in sessions) {
			DoBrowserRow(new Rect(0f, y, viewRect.width, _ROW_HEIGHT), session.Record, session);
			y += _ROW_HEIGHT;
		}
		for (int i = records.Count - 1; i >= 0; i--) {
			DoBrowserRow(new Rect(0f, y, viewRect.width, _ROW_HEIGHT), records[i], null);
			y += _ROW_HEIGHT;
		}
		Widgets.EndScrollView();
	}

	private List<BattleDossierRecord> Filtered(List<BattleDossierRecord> records) {
		if (_search.NullOrEmpty())
			return records;
		return records
			.Where(r => DisplayName(r).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
				|| r.EnemyFactionNames.Any(f => f.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
			)
			.ToList();
	}

	private void DoBrowserRow(Rect rect, BattleDossierRecord record, BattleSession? session) {
		bool isSelected = session != null ? _selectedSession == session : _selected == record && _selectedSession == null;
		if (isSelected)
			Widgets.DrawHighlightSelected(rect);
		else if (Mouse.IsOver(rect))
			Widgets.DrawHighlight(rect);

		var labelRect = rect.ContractedBy(2f);
		string label;
		if (session != null) {
			GUI.color = _inProgressColor;
			label = "BattleDossier.Browser.InProgress".Translate(session.BattleName());
		}
		else
			label = DisplayName(record);
		Text.Anchor = TextAnchor.MiddleLeft;
		Widgets.Label(labelRect, label.Truncate(labelRect.width - 30f));
		Text.Anchor = TextAnchor.UpperLeft;
		GUI.color = Color.white;
		if (session == null && record.Pinned)
			GUI.DrawTexture(new Rect(rect.xMax - 48f, rect.y + 4f, 20f, 20f), _pinTex);

		TooltipHandler.TipRegion(rect, () => RowTooltip(record), record.Id ^ 0x42D0);

		if (Widgets.ButtonInvisible(rect)) {
			_selected = record;
			_selectedSession = session;
			ResetFilters();
		}

		if (session != null || !Mouse.IsOver(rect))
			return;
		var deleteRect = new Rect(rect.xMax - 24f, rect.y + 2f, 22f, 22f);
		if (!Widgets.ButtonImage(deleteRect, TexButton.Delete))
			return;
		if (record.Pinned) {
			Messages.Message("BattleDossier.Browser.UnpinFirst".Translate(), MessageTypeDefOf.RejectInput, false);
			return;
		}
		var toDelete = record;
		Find.WindowStack.Add(
			Dialog_MessageBox.CreateConfirmation(
				"BattleDossier.Browser.DeleteConfirm".Translate(DisplayName(toDelete)),
				() => {
					Manager?.DeleteRecord(toDelete);
					if (_selected == toDelete)
						_selected = null;
				},
				true
			)
		);
	}
	// #endregion

	// #region Detail
	private void DoDetail(Rect rect, BattleDossierRecord record, bool live) {
		EnsureStats(record, live ? _selectedSession : null);

		Text.Font = GameFont.Medium;
		var titleRect = new Rect(rect.x, rect.y, rect.width - 220f, _HEADER_HEIGHT);
		string title = record.CustomName ? record.Name : live ? _selectedSession!.BattleName() : record.Name.StripTags();
		Widgets.Label(titleRect, title.Truncate(titleRect.width));
		Text.Font = GameFont.Small;

		var renameRect = new Rect(rect.xMax - 60f, rect.y + 4f, 24f, 24f);
		if (Widgets.ButtonImage(renameRect, TexButton.Rename))
			Find.WindowStack.Add(new RenameBattleDossierDialog(record));
		TooltipHandler.TipRegionByKey(renameRect, "BattleDossier.Detail.RenameTooltip");

		if (!live) {
			var pinRect = new Rect(rect.xMax - 30f, rect.y + 4f, 24f, 24f);
			Widgets.DrawTextureFitted(pinRect, record.Pinned ? _pinTex : _pinOutlineTex, 1f);
			if (Widgets.ButtonInvisible(pinRect))
				record.Pinned = !record.Pinned;
			TooltipHandler.TipRegionByKey(pinRect, "BattleDossier.Detail.PinTooltip");

			var outcomeRect = new Rect(rect.xMax - 210f, rect.y + 6f, 140f, 24f);
			GUI.color = record.Outcome switch {
				BattleOutcome.Victory => new Color(0.5f, 1f, 0.5f),
				BattleOutcome.Defeat  => new Color(1f, 0.4f, 0.4f),
				_                     => Color.gray
			};
			Text.Anchor = TextAnchor.MiddleRight;
			Widgets.Label(outcomeRect, $"BattleDossier.Outcome.{record.Outcome}".Translate());
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
		}

		var tabRect = new Rect(rect.x, rect.y + _HEADER_HEIGHT + 24f, rect.width, 0f);
		var tabs = new List<TabRecord> {
			new("BattleDossier.Tab.Overview".Translate(), () => _tab = Tab.Overview, _tab == Tab.Overview),
			new("BattleDossier.Tab.Leaderboard".Translate(), () => _tab = Tab.Leaderboard, _tab == Tab.Leaderboard),
			new("BattleDossier.Tab.Timeline".Translate(), () => _tab = Tab.Timeline, _tab == Tab.Timeline)
		};
		TabDrawer.DrawTabs(tabRect, tabs);

		var contentRect = new Rect(rect.x, tabRect.y + 6f, rect.width, rect.yMax - tabRect.y - 6f);
		switch (_tab) {
			case Tab.Overview:    DoOverview(contentRect, record, live); break;
			case Tab.Leaderboard: DoLeaderboard(contentRect, record); break;
			case Tab.Timeline:    DoTimeline(contentRect, record); break;
		}
	}
	// #endregion

	// #region Overview tab
	private void DoOverview(Rect rect, BattleDossierRecord record, bool live) {
		var participants = _statsParticipants.Values.ToList();
		float y = rect.y;

		void Line(string text) {
			Widgets.Label(new Rect(rect.x, y, rect.width, Text.LineHeight), text);
			y += Text.LineHeight;
		}

		void Divider() {
			y += 4f;
			Widgets.DrawLineHorizontal(rect.x, y, rect.width);
			y += 8f;
		}

		if (!record.BeganDate.NullOrEmpty())
			Line("BattleDossier.Overview.Began".Translate(record.BeganDate));
		int endTick = live ? Find.TickManager.TicksGame : record.EndTick;
		Line("BattleDossier.Overview.Duration".Translate((endTick - record.StartTick).ToStringTicksToPeriod()));
		if (record.MapNames.Count > 0)
			Line("BattleDossier.Overview.Maps".Translate(record.MapNames.ToCommaList()));
		float enemyPower = participants.Where(p => p.Side == BattleSide.Enemy).Sum(p => p.CombatPower);
		Line("BattleDossier.Overview.EnemyCombatPower".Translate(enemyPower.ToString("F0")));
		Divider();

		Text.Font = GameFont.Tiny;
		Line("BattleDossier.Overview.CasualtyHeader".Translate());
		Text.Font = GameFont.Small;
		y = DoCasualtyTable(rect, y, participants);
		Divider();

		Line("BattleDossier.Overview.TotalDamage".Translate(_stats.TotalDamage.ToString("F0")));
		if (_stats.UnattributedDamage > 0f)
			Line("BattleDossier.Overview.UnattributedDamage".Translate(_stats.UnattributedDamage.ToString("F0")));
	}

	private float DoCasualtyTable(Rect area, float y, List<ParticipantInfo> participants) {
		const float countCol = 80f;
		const float deadCol = 50f;
		const float downedCol = 60f;
		const float capturedCol = 70f;
		const float fledCol = 50f;
		float factionCol = area.width - (countCol + deadCol + downedCol + capturedCol + fledCol);

		void Row(Rect rowRect, string faction, string count, string dead, string downed, string captured, string fled) {
			float x = rowRect.x;
			DrawCell(ref x, rowRect, factionCol, faction);
			DrawCell(ref x, rowRect, countCol, count);
			DrawCell(ref x, rowRect, deadCol, dead);
			DrawCell(ref x, rowRect, downedCol, downed);
			DrawCell(ref x, rowRect, capturedCol, captured);
			DrawCell(ref x, rowRect, fledCol, fled);
		}

		Text.Font = GameFont.Tiny;
		Row(
			new Rect(area.x, y, area.width, 22f),
			"BattleDossier.Casualty.Faction".Translate(),
			"BattleDossier.Casualty.Participants".Translate(),
			"BattleDossier.Casualty.Dead".Translate(),
			"BattleDossier.Casualty.Downed".Translate(),
			"BattleDossier.Casualty.Captured".Translate(),
			"BattleDossier.Casualty.Fled".Translate()
		);
		Text.Font = GameFont.Small;
		y += 24f;

		var groups = participants.GroupBy(p => p.FactionName).OrderBy(g => g.Min(p => (int)p.Side)).ThenBy(g => g.Key);
		Text.Anchor = TextAnchor.MiddleLeft;
		foreach (var group in groups) {
			var members = group.ToList();
			var rowRect = new Rect(area.x, y, area.width, _ROW_HEIGHT);
			if (Mouse.IsOver(rowRect))
				Widgets.DrawHighlight(rowRect);
			string faction = group.Key.NullOrEmpty() ? "BattleDossier.Side.Wild".Translate() : group.Key;
			Row(
				rowRect,
				faction.Truncate(factionCol - 6f),
				members.Count.ToString(),
				Count(members, ParticipantFate.Dead),
				Count(members, ParticipantFate.Downed),
				Count(members, ParticipantFate.Captured),
				Count(members, ParticipantFate.Fled)
			);
			y += _ROW_HEIGHT;
		}
		Text.Anchor = TextAnchor.UpperLeft;
		return y;

		string Count(List<ParticipantInfo> members, ParticipantFate fate) {
			int n = members.Count(p => _stats.Summaries[p.ThingId].Fate == fate);
			return n == 0 ? "-" : n.ToString();
		}
	}
	// #endregion

	// #region Leaderboard tab
	private void DoLeaderboard(Rect rect, BattleDossierRecord record) {
		var filterRow = new WidgetRow(rect.x, rect.y, UIDirection.RightThenDown, rect.width);
		string factionLabel = _factionFilter == null
			? "BattleDossier.Filter.All".Translate()
			: _factionFilter.Length == 0
				? "BattleDossier.Side.Wild".Translate()
				: _factionFilter;
		if (filterRow.ButtonText("BattleDossier.Filter.Faction".Translate(factionLabel)))
			OpenFactionMenu();
		string typeLabel = _typeFilter == null ? "BattleDossier.Filter.All".Translate() : $"BattleDossier.Kind.{_typeFilter}".Translate();
		if (filterRow.ButtonText("BattleDossier.Filter.Type".Translate(typeLabel)))
			OpenTypeMenu();

		var rows = _statsParticipants.Values.Select(p => new Row(p, _stats.Summaries[p.ThingId]));
		if (_factionFilter != null)
			rows = rows.Where(r => r.Info.FactionName == _factionFilter);
		if (_typeFilter is { } type)
			rows = rows.Where(r => r.Info.Kind == type);
		var list = rows.ToList();
		var column = _columns[_sortColumn];
		list.Sort(_sortDescending ? (a, b) => column.Comparison(b, a) : column.Comparison);

		var headerRect = new Rect(rect.x, rect.y + 34f, rect.width - 16f, 26f);
		float fixedWidth = _columns.Skip(1).Sum(c => c.Width);
		float nameWidth = headerRect.width - fixedWidth;
		float x = headerRect.x;
		Text.Font = GameFont.Tiny;
		for (var i = 0; i < _columns.Length; i++) {
			float width = i == 0 ? nameWidth : _columns[i].Width;
			var cell = new Rect(x, headerRect.y, width, headerRect.height);
			string label = $"BattleDossier.Column.{_columns[i].Key}".Translate();
			if (i == _sortColumn)
				label += _sortDescending ? " v" : " ^";
			if (Widgets.ButtonText(cell, label, false)) {
				if (_sortColumn == i)
					_sortDescending = !_sortDescending;
				else
					(_sortColumn, _sortDescending) = (i, true);
			}
			x += width;
		}
		Text.Font = GameFont.Small;

		var bodyRect = new Rect(rect.x, headerRect.yMax + 2f, rect.width, rect.yMax - headerRect.yMax - 2f);
		bool split = _selectedParticipantId >= 0 && _statsParticipants.ContainsKey(_selectedParticipantId);
		var tableRect = split ? bodyRect.TopPartPixels(bodyRect.height / 2f - 4f) : bodyRect;

		var viewRect = new Rect(0f, 0f, tableRect.width - 16f, list.Count * _ROW_HEIGHT);
		Widgets.BeginScrollView(tableRect, ref _contentScroll, viewRect);
		var y = 0f;
		foreach (var row in list) {
			bool isSel = row.Info.ThingId == _selectedParticipantId;
			if (DoLeaderboardRow(new Rect(0f, y, viewRect.width, _ROW_HEIGHT), row, nameWidth, isSel))
				_selectedParticipantId = isSel ? -1 : row.Info.ThingId;
			y += _ROW_HEIGHT;
		}
		Widgets.EndScrollView();

		if (split)
			DoParticipantDetail(bodyRect.BottomPartPixels(bodyRect.height / 2f - 4f), record);
	}

	private void DoParticipantDetail(Rect rect, BattleDossierRecord record) {
		var info = _statsParticipants[_selectedParticipantId];
		Widgets.DrawLineHorizontal(rect.x, rect.y - 4f, rect.width);
		var summary = _stats.Summaries[_selectedParticipantId];
		var headerRect = new Rect(rect.x, rect.y, rect.width, Text.LineHeight);
		Widgets.Label(
			headerRect,
			$"{NameWithKind(info)}  —  "
			+ "BattleDossier.Detail.ParticipantSummary".Translate(
				summary.DamageDealt.ToString("F0"),
				summary.Kills,
				summary.Downs,
				summary.DamageTaken.ToString("F0")
			)
		);
		var listRect = new Rect(rect.x, headerRect.yMax + 2f, rect.width, rect.yMax - headerRect.yMax - 2f);
		var logs = record.Logs.Where(r => r.Concerns(_selectedParticipantId)).ToList();
		DoLogList(listRect, logs, record.StartTick, true, ref _detailScroll);
	}

	private void OpenFactionMenu() {
		var options = new List<FloatMenuOption> { new("BattleDossier.Filter.All".Translate(), () => _factionFilter = null) };
		foreach (string faction in _statsParticipants.Values.Select(p => p.FactionName).Distinct().OrderBy(s => s)) {
			string value = faction;
			string label = faction.NullOrEmpty() ? "BattleDossier.Side.Wild".Translate() : faction;
			options.Add(new FloatMenuOption(label, () => _factionFilter = value));
		}
		Find.WindowStack.Add(new FloatMenu(options));
	}

	private void OpenTypeMenu() {
		var options = new List<FloatMenuOption> { new("BattleDossier.Filter.All".Translate(), () => _typeFilter = null) };
		foreach (var participantType in _statsParticipants.Values.Select(p => p.Kind).Distinct().OrderBy(k => (int)k)) {
			var value = participantType;
			options.Add(new FloatMenuOption($"BattleDossier.Kind.{participantType}".Translate(), () => _typeFilter = value));
		}
		Find.WindowStack.Add(new FloatMenu(options));
	}
	// #endregion

	// #region Timeline tab
	private void DoTimeline(Rect rect, BattleDossierRecord record) {
		var toggleRect = new Rect(rect.x, rect.y, rect.width, 24f);
		Widgets.CheckboxLabeled(toggleRect, "BattleDossier.Timeline.ShowNonLethal".Translate(), ref _showNonLethal);
		var listRect = new Rect(rect.x, toggleRect.yMax + 4f, rect.width, rect.yMax - toggleRect.yMax - 4f);
		DoLogList(listRect, record.Logs, record.StartTick, _showNonLethal, ref _contentScroll);
	}

	private void DoLogList(Rect rect, IReadOnlyList<DossierLog> logs, int startTick, bool showHits, ref Vector2 scroll) {
		var visible = new List<DossierLog>(logs.Count);
		foreach (var log in logs) {
			if (showHits || log is not HitLog)
				visible.Add(log);
		}
		float textWidth = rect.width - _LOG_TEXT_X - 16f;
		var texts = new string[visible.Count];
		var heights = new float[visible.Count];
		var viewHeight = 0f;
		for (var i = 0; i < visible.Count; i++) {
			texts[i] = visible[i].Describe(_statsParticipants);
			heights[i] = Mathf.Max(_ROW_HEIGHT, Text.CalcHeight(texts[i], textWidth));
			viewHeight += heights[i];
		}

		var viewRect = new Rect(0f, 0f, rect.width - 16f, viewHeight);
		Widgets.BeginScrollView(rect, ref scroll, viewRect);
		var y = 0f;
		for (var i = 0; i < visible.Count; i++) {
			float h = heights[i];
			var rowRect = new Rect(0f, y, viewRect.width, h);
			if (Mouse.IsOver(rowRect))
				Widgets.DrawHighlight(rowRect);
			GUI.color = Color.gray;
			Widgets.Label(new Rect(0f, y, _TIME_WIDTH, h), (visible[i].Tick - startTick).ToStringTicksToPeriod(false, true));
			var (icon, color) = Style(visible[i]);
			if (icon != null) {
				GUI.color = Color.white;
				GUI.DrawTexture(new Rect(_TIME_WIDTH + 4f, y + (h - 18f) / 2f, 18f, 18f), icon);
			}
			GUI.color = color;
			Widgets.Label(new Rect(_LOG_TEXT_X, y, viewRect.width - _LOG_TEXT_X, h), texts[i]);
			GUI.color = Color.white;
			y += h;
		}
		Widgets.EndScrollView();
	}
	// #endregion

	private void ResetFilters() {
		_factionFilter = null;
		_typeFilter = null;
		_contentScroll = Vector2.zero;
		_detailScroll = Vector2.zero;
		_selectedParticipantId = -1;
		_tab = Tab.Overview;
	}

	private readonly struct Row(ParticipantInfo info, ParticipantSummary summary) {
		public ParticipantInfo Info { get; } = info;

		public ParticipantSummary Summary { get; } = summary;
	}

	private readonly struct Column(string key, float width, Comparison<Row> comparison) {
		public string Key { get; } = key;

		public float Width { get; } = width;

		public Comparison<Row> Comparison { get; } = comparison;
	}
}