using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using TrueMogician.RimWorld.BattleDossier.Models;
using TrueMogician.RimWorld.BattleDossier.Static;
using TrueMogician.RimWorld.BattleDossier.UI;
using TrueMogician.RimWorld.Utility;
using Verse;

namespace TrueMogician.RimWorld.BattleDossier.Components;

using static Helper;

public class DossierManager : GameComponent {
	private const int _END_POLL_INTERVAL = 250;

	private static readonly Lazy<KeyBindingDef?> _openBrowserKey =
		new(() => DefDatabase<KeyBindingDef>.GetNamedSilentFail("BattleDossier_OpenBrowser"));

	private List<BattleSession> _activeSessions = [];
	private List<BattleDossierRecord> _records = [];
	private int _nextRecordId;

	public DossierManager(Game _) { }

	/// <summary>Cheap gate read by every collection patch before doing any work.</summary>
	public static bool AnySessionActive { get; private set; }

	public static DossierManager? Instance => CachedGameComponent<DossierManager>.TryGet();

	public IReadOnlyList<BattleSession> ActiveSessions => _activeSessions;

	public List<BattleDossierRecord> Records => _records;

	public BattleSession? FindSession(Battle battle) {
		foreach (var session in _activeSessions) {
			if (session.Covers(battle))
				return session;
		}
		return null;
	}

	public BattleSession? SessionOnMap(Map? map) {
		if (map == null)
			return null;
		foreach (var session in _activeSessions) {
			if (session.Maps.Contains(map))
				return session;
		}
		return null;
	}

	public BattleSession? FindSessionFor(Thing thing) {
		foreach (var session in _activeSessions) {
			if (session.Participants.ContainsKey(thing.thingIDNumber))
				return session;
		}
		// Pawns also belong to a session through their vanilla battle membership.
		if (thing is Pawn pawn && pawn.records.BattleActive is { } battle)
			return FindSession(battle);
		return null;
	}

	public BattleSession StartSession(Battle battle, Map? map) {
		var session = new BattleSession(battle, map, _nextRecordId++);
		_activeSessions.Add(session);
		// Concurrent-battle heuristic: merge sessions simultaneously active on a shared map.
		for (int i = _activeSessions.Count - 2; i >= 0; i--) {
			var other = _activeSessions[i];
			if (map != null && other.Maps.Contains(map)) {
				other.MergeFrom(session);
				_activeSessions.Remove(session);
				AnySessionActive = _activeSessions.Count > 0;
				return other;
			}
		}
		AnySessionActive = true;
		Logger.Message($"Session started: {session.Record.GetUniqueLoadID()}");
		return session;
	}

	/// <summary>Re-merges sessions bridged by a vanilla <c>Battle.Absorb</c> (same root battle in two sessions).</summary>
	public void ConsolidateSessions() {
		for (var i = 0; i < _activeSessions.Count; i++) {
			for (int j = _activeSessions.Count - 1; j > i; j--) {
				if (SharesRoot(_activeSessions[i], _activeSessions[j])) {
					_activeSessions[i].MergeFrom(_activeSessions[j]);
					_activeSessions.RemoveAt(j);
				}
			}
		}
		AnySessionActive = _activeSessions.Count > 0;
	}

	public override void GameComponentOnGUI() {
		if (_openBrowserKey.Value?.KeyDownEvent == true)
			Find.MainTabsRoot.ToggleTab(BattleDossierDefOf.BattleDossier);
	}

	public override void GameComponentTick() {
		if (_activeSessions.Count == 0 || Find.TickManager.TicksGame % _END_POLL_INTERVAL != 0)
			return;
		for (int i = _activeSessions.Count - 1; i >= 0; i--) {
			var session = _activeSessions[i];
			if (session.ShouldEnd())
				EndSession(session, ClassifyOutcome(session));
		}
	}

	public void EndSession(BattleSession session, BattleOutcome outcome, bool notify = true) {
		_activeSessions.Remove(session);
		AnySessionActive = _activeSessions.Count > 0;
		var record = session.Complete(outcome);
		_records.Add(record);
		ApplyRollingWindow();
		Logger.Message($"Session ended: {record.Name} ({outcome}, {record.Participants.Count} participants)");
		if (!notify)
			return;
		SendLetter(record);
		if (!Settings.Default.AutoOpenWindow)
			return;
		BattleDossierWindow.Open(record);
		Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
	}

	public void DeleteRecord(BattleDossierRecord record) => _records.Remove(record);

	public override void LoadedGame() {
		// Sessions whose vanilla battles were all trimmed before saving cannot continue meaningfully.
		for (int i = _activeSessions.Count - 1; i >= 0; i--) {
			if (_activeSessions[i].LostAllBattles)
				EndSession(_activeSessions[i], BattleOutcome.Expired, false);
		}
		AnySessionActive = _activeSessions.Count > 0;
	}

	public override void ExposeData() {
		Scribe_Collections.Look(ref _activeSessions, "activeSessions", LookMode.Deep);
		Scribe_Collections.Look(ref _records, "records", LookMode.Deep);
		Scribe_Values.Look(ref _nextRecordId, "nextRecordId");
		if (Scribe.mode != LoadSaveMode.PostLoadInit)
			return;
		_activeSessions ??= [];
		_records ??= [];
		AnySessionActive = _activeSessions.Count > 0;
	}

	private static bool SharesRoot(BattleSession a, BattleSession b) {
		foreach (var battle in Find.BattleLog.Battles) {
			if (battle.AbsorbedBy == null && a.Covers(battle) && b.Covers(battle))
				return true;
		}
		return false;
	}

	private static BattleOutcome ClassifyOutcome(BattleSession session) {
		foreach (var map in session.Maps) {
			if (map is { Disposed: false } && GenHostility.AnyHostileActiveThreatToPlayer(map))
				return BattleOutcome.Expired;
		}
		bool anyColonistFree = session.Participants.Values.Any(p => p is { Side: BattleSide.Colony, LiveThing: Pawn { Downed: false, Dead: false } });
		return anyColonistFree ? BattleOutcome.Victory : BattleOutcome.Defeat;
	}

	private static void SendLetter(BattleDossierRecord record) {
		var def = DefDatabase<LetterDef>.GetNamed(record.Outcome == BattleOutcome.Victory ? "BattleDossier_Victory" : "BattleDossier_Neutral");
		var letter = (BattleEndedLetter)LetterMaker.MakeLetter(
			"BattleDossier.Letter.Label".Translate(record.Name),
			"BattleDossier.Letter.Text".Translate(record.Name),
			def
		);
		letter.Record = record;
		Find.LetterStack.ReceiveLetter(letter);
	}

	private void ApplyRollingWindow() {
		int max = Settings.Default.MaxStoredDossiers;
		if (max <= 0)
			return;
		var unpinned = _records.Where(r => !r.Pinned).ToList();
		for (var i = 0; unpinned.Count - i > max; i++)
			_records.Remove(unpinned[i]);
	}
}