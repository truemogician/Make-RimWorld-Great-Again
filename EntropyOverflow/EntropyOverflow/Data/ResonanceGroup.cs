using System;
using System.Collections.Generic;
using System.Linq;
using TrueMogician.RimWorld.Utility;
using Verse;

namespace TrueMogician.RimWorld.EntropyOverflow.Data;

[Flags]
public enum ResonanceGroupFlags {
	None = 0,
	BrokenDown = 1 << 0
}

public class ResonanceGroup : IExposable {
	public const byte MAX_LEVEL = 5;

	public const float JOIN_XP_PENALTY = 0.8f;

	public const float QUIT_XP_PENALTY = 0.5f;

	public const byte DEATH_LEVEL_PENALTY = 2;

	public const byte RESURRECTION_LEVEL_COST = 3;

	public static readonly float[] XpThresholds = [0f, 100f, 300f, 700f, 1500f, 3100f];

	private readonly List<Record> _history = [];

	private readonly HashSet<Pawn> _members = [];

	private ResonanceGroupFlags _flags = ResonanceGroupFlags.None;

	private int _id;

	private byte _level;

	private int _resonatorId;

	private float _xp;

	public ResonanceGroup() { }

	internal ResonanceGroup(int id, int resonatorId, IEnumerable<Pawn> pawns, int tick = -1) {
		_id = id;
		_resonatorId = resonatorId;
		_level = 1;
		tick = tick < 0 ? Find.TickManager.TicksGame : tick;
		_members = pawns.ToHashSet();
		if (_members.Count == 0)
			throw new ArgumentException("A resonance group must have at least one member.", nameof(pawns));
		if (_members.Any(p => p.Dead))
			throw new ArgumentException("A resonance group cannot have dead pawns as members.", nameof(pawns));
		_history = _members
			.Select(Record (p) => new MemberRecord {
					Tick = tick,
					Action = MemberAction.Creation,
					Pawn = p
				}
			)
			.ToList();
	}

	public enum BreakdownReason : byte {
		Voluntary = 1,
		Death
	}

	public enum LevelReason : byte {
		Debug = 1,
		Upgrade,
		Downgrade,
		MemberJoin,
		MemberQuit,
		MemberDeath
	}

	public enum MemberAction : byte {
		Creation = 1,
		Join,
		Quit,
		Death
	}

	public enum RecordType : byte {
		Member = 1,
		Level,
		Breakdown
	}

	public void ExposeData() {
		Scribe_Values.Look(ref _id, "id");
		Scribe_Values.Look(ref _resonatorId, "resonator");
		Scribe_Values.Look(ref _level, "level");
		Scribe_Values.Look(ref _xp, "xp");
		if (_level == 0 || _id == 0 || _resonatorId == 0)
			throw new CorruptedDataException(typeof(ResonanceGroup));
		var members = _members.ToList();
		var records = _history.ToList();
		Scribe_Collections.Look(ref members, "members", LookMode.Reference);
		Scribe_Collections.Look(ref records, "history", LookMode.Deep);
		if (members is null || records is null)
			throw new CorruptedDataException(typeof(ResonanceGroup));
		if (Scribe.mode == LoadSaveMode.LoadingVars) {
			_members.Clear();
			members.ForEach(p => _members.Add(p));
			_history.Clear();
			_history.AddRange(records);
			if (!VerifyHistory())
				throw new CorruptedDataException("Inconsistent resonance group history.", typeof(ResonanceGroup));
		}
	}

	public abstract record Record : IExposable {
		private int _tick;

		public virtual void ExposeData() {
			Scribe_Values.Look(ref _tick, "tick");
			if (_tick == 0)
				throw new CorruptedDataException(typeof(Record));
		}

		public abstract RecordType Type { get; }

		public int Tick {
			get => _tick;
			init => _tick = value;
		}
	}

	public record MemberRecord : Record {
		private MemberAction _action;

		private Pawn _pawn = null!;

		public MemberAction Action {
			get => _action;
			init => _action = value;
		}

		public Pawn Pawn {
			get => _pawn;
			init => _pawn = value;
		}

		public override RecordType Type => RecordType.Member;

		public override void ExposeData() {
			base.ExposeData();
			var type = Type;
			Scribe_Values.Look(ref type, "type");
			Scribe_Values.Look(ref _action, "action");
			Scribe_References.Look(ref _pawn, "pawn");
			if (type != RecordType.Member || _action == 0 || _pawn is null)
				throw new CorruptedDataException(typeof(MemberRecord));
		}
	}

	public record LevelRecord : Record {
		private LevelReason _reason;
		private sbyte _value;

		public sbyte Value {
			get => _value;
			init => _value = value;
		}

		public LevelReason Reason {
			get => _reason;
			init => _reason = value;
		}

		public override RecordType Type => RecordType.Level;

		public override void ExposeData() {
			base.ExposeData();
			var type = Type;
			Scribe_Values.Look(ref type, "type");
			Scribe_Values.Look(ref _value, "value");
			Scribe_Values.Look(ref _reason, "reason");
			if (type != RecordType.Level || _value == 0 || _reason == 0)
				throw new CorruptedDataException(typeof(LevelRecord));
		}
	}

	public record BreakdownRecord : Record {
		private BreakdownReason _reason;

		public BreakdownReason Reason {
			get => _reason;
			init => _reason = value;
		}

		public override RecordType Type => RecordType.Breakdown;

		public override void ExposeData() {
			base.ExposeData();
			var type = Type;
			Scribe_Values.Look(ref type, "type");
			Scribe_Values.Look(ref _reason, "reason");
			if (type != RecordType.Breakdown || _reason == 0)
				throw new CorruptedDataException(typeof(BreakdownRecord));
		}
	}

	public int Id => _id;

	public int ResonatorId => _resonatorId;

	public byte Level => _level;

	public float Xp {
		get => _xp;
		internal set => _xp = Math.Clamp(value, 0, XpThresholds[_level]);
	}

	public float CurrentMaxXp => XpThresholds[_level];

	public bool UpgradeAvailable => _level < MAX_LEVEL && _xp >= CurrentMaxXp;

	public bool IsBrokenDown => _flags.HasFlag(ResonanceGroupFlags.BrokenDown);

	public IReadOnlyList<Record> History => _history;

	public IReadOnlyCollection<Pawn> Members => _members;

	public bool Upgrade(int tick = -1) {
		if (!UpgradeAvailable)
			return false;
		UpdateLevel(1, LevelReason.Upgrade, tick);
		_xp = 0;
		return true;
	}

	public bool Join(Pawn pawn, int tick = -1) {
		if (_members.Contains(pawn) || pawn.Dead)
			return false;
		tick = tick < 0 ? Find.TickManager.TicksGame : tick;
		if (!ApplyXpPenalty(JOIN_XP_PENALTY, LevelReason.MemberJoin, tick))
			return false;
		_members.Add(pawn);
		_history.Add(
			new MemberRecord {
				Tick = tick,
				Action = MemberAction.Join,
				Pawn = pawn
			}
		);
		return true;
	}

	public bool Quit(Pawn pawn, int tick = -1) {
		if (!_members.Contains(pawn) || pawn.Dead)
			return false;
		tick = tick < 0 ? Find.TickManager.TicksGame : tick;
		if (!ApplyXpPenalty(QUIT_XP_PENALTY, LevelReason.MemberQuit, tick))
			return false;
		_members.Remove(pawn);
		_history.Add(
			new MemberRecord {
				Tick = tick,
				Action = MemberAction.Quit,
				Pawn = pawn
			}
		);
		return true;
	}

	public bool Death(Pawn pawn, int tick = -1) {
		if (!_members.Contains(pawn) || !pawn.Dead)
			return false;
		tick = tick < 0 ? Find.TickManager.TicksGame : tick;
		if (_level <= DEATH_LEVEL_PENALTY)
			Breakdown(BreakdownReason.Death, tick);
		else {
			UpdateLevel(-DEATH_LEVEL_PENALTY, LevelReason.MemberDeath, tick);
			_xp = 0;
			_members.Remove(pawn);
			_history.Add(
				new MemberRecord {
					Tick = tick,
					Action = MemberAction.Death,
					Pawn = pawn
				}
			);
		}
		return true;
	}

	public void Breakdown(BreakdownReason reason, int tick = -1) {
		if (IsBrokenDown)
			throw new InvalidOperationException("Resonance group is already broken down.");
		tick = tick < 0 ? Find.TickManager.TicksGame : tick;
		_flags |= ResonanceGroupFlags.BrokenDown;
		_history.Add(
			new BreakdownRecord {
				Tick = tick,
				Reason = reason
			}
		);
	}

	private bool ApplyXpPenalty(float percentage, LevelReason reason, int tick = -1) {
		float xpPenalty = percentage * CurrentMaxXp;
		if (xpPenalty > _xp && _level == 1)
			return false;
		if (xpPenalty > _xp)
			UpdateLevel(-1, reason, tick);
		_xp = Math.Max(0, _xp - xpPenalty);
		return true;
	}

	private void UpdateLevel(sbyte change, LevelReason reason, int tick = -1) {
		var newLevel = _level + change;
		if (newLevel is < 1 or > MAX_LEVEL)
			throw new ArgumentOutOfRangeException(nameof(change), "Resulting level is out of bounds.");
		if (newLevel == _level)
			return;
		tick = tick < 0 ? Find.TickManager.TicksGame : tick;
		_level = (byte)newLevel;
		_history.Add(
			new LevelRecord {
				Tick = tick,
				Value = change,
				Reason = reason
			}
		);
	}

	internal bool VerifyHistory() {
		var memberSet = new HashSet<Pawn>();
		byte level = 1;
		var breakdown = false;
		foreach (var record in _history) {
			if (breakdown)
				return false;
			switch (record) {
				case MemberRecord mr:
					switch (mr.Action) {
						case MemberAction.Creation:
						case MemberAction.Join:
							if (!memberSet.Add(mr.Pawn))
								return false;
							break;
						case MemberAction.Quit:
						case MemberAction.Death:
							if (!memberSet.Remove(mr.Pawn))
								return false;
							break;
					}
					break;
				case LevelRecord lr: {
					var newLevel = (sbyte)(level + lr.Value);
					if (newLevel < 1 || newLevel > MAX_LEVEL)
						return false;
					level = (byte)newLevel;
					break;
				}
				case BreakdownRecord br: breakdown = true; break;
				default:                 return false;
			}
		}
		return level == _level;
	}
}