using System.Collections.Generic;
using System.Linq;
using RimWorld;
using TrueMogician.RimWorld.BattleDossier.Core;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.BattleDossier.Models;

/// <summary>
///     Live accumulator for an ongoing battle, keyed by a set of vanilla <see cref="Battle" /> objects
///     (multiple when concurrent engagements were merged). Appends to the <see cref="BattleDossierRecord" />'s
///     log incrementally and finalizes it on battle end; the leaderboard and timeline are derived from it.
/// </summary>
public sealed class BattleSession : IExposable {
	private List<Battle> _battles = [];

	// A single vanilla battle can span maps: a pawn fighting on one map then another within the
	// exit window bridges both into the same battle. End-of-battle threat checks scan every map here.
	private List<Map> _maps = [];

	private Dictionary<int, ParticipantInfo> _participants = [];
	private BattleDossierRecord _record = new();
	private int _lastActivityTick;
	private int _lastThreatTick;

	public BattleSession() { }

	public BattleSession(Battle battle, Map? map, int id) {
		_battles.Add(battle);
		if (map != null)
			_maps.Add(map);
		_record.Id = id;
		_record.StartTick = Find.TickManager.TicksGame;
		_lastActivityTick = _record.StartTick;
		_lastThreatTick = _record.StartTick;
		_record.Outcome = BattleOutcome.InProgress;
		if (map != null)
			_record.MapNames.Add(MapLabel(map));
		AddMarker(MarkerType.BattleStarted);
	}

	public void ExposeData() {
		Scribe_Collections.Look(ref _battles, "battles", LookMode.Reference);
		Scribe_Collections.Look(ref _maps, "maps", LookMode.Reference);
		Scribe_Collections.Look(ref _participants, "participants", LookMode.Value, LookMode.Deep);
		Scribe_Deep.Look(ref _record, "record");
		Scribe_Values.Look(ref _lastActivityTick, "lastActivityTick");
		Scribe_Values.Look(ref _lastThreatTick, "lastThreatTick");
		if (Scribe.mode != LoadSaveMode.PostLoadInit)
			return;
		_battles ??= [];
		_battles.RemoveAll(b => b == null);
		_maps ??= [];
		_maps.RemoveAll(m => m == null);
		_record ??= new BattleDossierRecord();
		_participants ??= [];
	}

	public BattleDossierRecord Record => _record;

	public IReadOnlyList<Map> Maps => _maps;

	public IReadOnlyDictionary<int, ParticipantInfo> Participants => _participants;

	/// <summary>True when every underlying vanilla battle reference was lost (e.g. trimmed before a save).</summary>
	public bool LostAllBattles => _battles.Count == 0;

	public static BattleSide SideOf(Thing thing) {
		var faction = thing.Faction;
		if (faction == null)
			return BattleSide.Wild;
		if (faction.IsPlayer)
			return BattleSide.Colony;
		return faction.HostileTo(Faction.OfPlayer) ? BattleSide.Enemy : BattleSide.Ally;
	}

	public static Battle Root(Battle battle) {
		while (battle.AbsorbedBy != null)
			battle = battle.AbsorbedBy;
		return battle;
	}

	/// <summary>Whether any of this session's battles (following absorb chains) matches <paramref name="battle" />.</summary>
	public bool Covers(Battle battle) {
		var root = Root(battle);
		foreach (var owned in _battles) {
			if (Root(owned) == root)
				return true;
		}
		return false;
	}

	public void AddBattle(Battle battle, Map? map) {
		if (!Covers(battle))
			_battles.Add(battle);
		NoticeMap(map);
	}

	public void NoticeMap(Map? map) {
		if (map == null || _maps.Contains(map))
			return;
		_maps.Add(map);
		string label = MapLabel(map);
		if (!_record.MapNames.Contains(label))
			_record.MapNames.Add(label);
	}

	// Stamped in game ticks; vanilla's Battle.LastEntryTimestamp is absolute ticks and not comparable here.
	public void NoticeActivity() => _lastActivityTick = Find.TickManager.TicksGame;

	public ParticipantInfo GetOrAddParticipant(Thing thing) {
		if (_participants.TryGetValue(thing.thingIDNumber, out var stats))
			return stats;
		stats = new ParticipantInfo(thing, SideOf(thing));
		_participants.Add(thing.thingIDNumber, stats);
		return stats;
	}

	public void AddHit(int instigatorId, int subjectId, float damage, bool hostile) =>
		_record.Logs.Add(new HitLog(Find.TickManager.TicksGame, instigatorId, subjectId, damage, hostile));

	public void AddCasualty(CasualtyType kind, int subjectId, int sourceId, bool hostile, string prose = "") =>
		_record.Logs.Add(new CasualtyLog(Find.TickManager.TicksGame, kind, subjectId, sourceId, hostile, prose));

	/// <summary>The instigator of the most recent hostile hit on <paramref name="subjectId" />, or -1.</summary>
	public int LastHostileHitter(int subjectId) {
		for (int i = _record.Logs.Count - 1; i >= 0; i--) {
			if (_record.Logs[i] is HitLog { Hostile: true } hit && hit.SubjectId == subjectId)
				return hit.InstigatorId;
		}
		return -1;
	}

	/// <summary>Merge another live session into this one (concurrent-battle heuristic or vanilla absorb bridging).</summary>
	public void MergeFrom(BattleSession other) {
		foreach (var battle in other._battles) {
			if (!Covers(battle))
				_battles.Add(battle);
		}
		foreach (var map in other._maps)
			NoticeMap(map);
		foreach (var pair in other._participants) {
			if (!_participants.ContainsKey(pair.Key))
				_participants.Add(pair.Key, pair.Value);
		}
		_lastActivityTick = Mathf.Max(_lastActivityTick, other._lastActivityTick);
		_lastThreatTick = Mathf.Max(_lastThreatTick, other._lastThreatTick);
		_record.StartTick = Mathf.Min(_record.StartTick, other._record.StartTick);
		_record.Logs.AddRange(other._record.Logs);
		_record.Logs.SortBy(e => e.Tick);
		AddMarker(MarkerType.FrontMerged, other.BattleName());
	}

	public string BattleName() {
		Battle? largest = null;
		foreach (var owned in _battles) {
			var root = Root(owned);
			if (largest == null || root.Importance > largest.Importance)
				largest = root;
		}
		return (largest?.GetName() ?? "BattleDossier.UnknownBattle".Translate()).StripTags();
	}

	/// <summary>Delegates to the <see cref="BattleEndConditions" /> registry over this session's live timing state.</summary>
	public bool ShouldEnd() {
		bool threat = AnyActiveThreat();
		if (threat)
			_lastThreatTick = Find.TickManager.TicksGame;
		var context = new BattleEndContext {
			Maps = _maps,
			LastThreatTick = threat ? -1 : _lastThreatTick,
			LastActivityTick = _lastActivityTick
		};
		return BattleEndConditions.Evaluate(in context);
	}

	public BattleDossierRecord Complete(BattleOutcome outcome) {
		if (!_record.CustomName)
			_record.Name = BattleName();
		_record.EndTick = Find.TickManager.TicksGame;
		_record.Outcome = outcome;
		SynthesizeMissingFates();
		_record.Participants = _participants.Values.ToList();
		AddMarker(MarkerType.BattleEnded);
		return _record;
	}

	private static string MapLabel(Map map) => map.Parent?.LabelCap ?? map.ToString();

	// Participants gone at battle end without a terminal event: enemy pawns fled, buildings were destroyed.
	private void SynthesizeMissingFates() {
		var resolved = new HashSet<int>(_record.Logs.OfType<CasualtyLog>().Select(c => c.SubjectId));
		foreach (var pair in _participants) {
			var stats = pair.Value;
			if (resolved.Contains(pair.Key))
				continue;
			if (stats.IsBuilding) {
				if (stats.LiveThing == null)
					AddCasualty(CasualtyType.Destroyed, pair.Key, -1, false);
			}
			else if (stats is { IsPlayerSide: false, LiveThing: null, LiveCorpse: null })
				AddCasualty(CasualtyType.Fled, pair.Key, -1, false);
		}
	}

	private void AddMarker(MarkerType kind, string text = "") =>
		_record.Logs.Add(new MarkerLog(Find.TickManager.TicksGame, kind, text));

	private bool AnyActiveThreat() {
		foreach (var map in _maps) {
			if (map is { Disposed: false } && GenHostility.AnyHostileActiveThreatToPlayer(map))
				return true;
		}
		return false;
	}
}