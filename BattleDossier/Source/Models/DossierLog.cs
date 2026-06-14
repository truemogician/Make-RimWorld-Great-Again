using System.Collections.Generic;
using Verse;

namespace TrueMogician.RimWorld.BattleDossier.Models;

/// <summary>
///     One entry in a battle's log. Concrete subclasses carry type-specific data; the leaderboard and
///     timeline are both derived from a list of these. Persisted polymorphically — under <c>LookMode.Deep</c>
///     RimWorld writes a <c>Class=</c> attribute per element and reconstructs the subclass, so each needs a
///     parameterless constructor.
/// </summary>
public abstract class DossierLog : IExposable {
	public int Tick;

	protected DossierLog() { }

	protected DossierLog(int tick) => Tick = tick;

	public virtual void ExposeData() => Scribe_Values.Look(ref Tick, "tick");

	/// <summary>Whether the log entry involves the given participant (by thing ID), for per-participant views.</summary>
	public abstract bool Concerns(int participantId);

	/// <summary>Renders the log entry to display text, resolving participant names from <paramref name="participants" />.</summary>
	public abstract string Describe(IReadOnlyDictionary<int, ParticipantInfo> participants);

	protected static string LabelOf(IReadOnlyDictionary<int, ParticipantInfo> participants, int id) {
		if (id >= 0 && participants.TryGetValue(id, out var stats))
			return stats.Label;
		return "BattleDossier.UnknownParticipant".Translate();
	}
}

/// <summary>
///     A unit of attributed damage from instigator to subject.
/// </summary>
public sealed class HitLog : DossierLog {
	public int InstigatorId = -1;
	public int SubjectId = -1;
	public float Damage;
	public bool Hostile;

	public HitLog() { }

	public HitLog(int tick, int instigatorId, int subjectId, float damage, bool hostile) : base(tick) {
		InstigatorId = instigatorId;
		SubjectId = subjectId;
		Damage = damage;
		Hostile = hostile;
	}

	public override bool Concerns(int participantId) => participantId == InstigatorId || participantId == SubjectId;

	public override string Describe(IReadOnlyDictionary<int, ParticipantInfo> participants) =>
		InstigatorId >= 0
			? "BattleDossier.Log.Hit".Translate(LabelOf(participants, InstigatorId), LabelOf(participants, SubjectId), Damage.ToString("F0"))
			: "BattleDossier.Log.HitUnattributed".Translate(LabelOf(participants, SubjectId), Damage.ToString("F0"));

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref InstigatorId, "instigator", -1);
		Scribe_Values.Look(ref SubjectId, "subject", -1);
		Scribe_Values.Look(ref Damage, "damage");
		Scribe_Values.Look(ref Hostile, "hostile");
	}
}

/// <summary>A terminal outcome for a participant: downed, killed, captured, fled, or (building) destroyed.</summary>
public sealed class CasualtyLog : DossierLog {
	public int SubjectId = -1;
	public int SourceId = -1;
	public CasualtyType Type;
	public bool Hostile;
	public string Prose = "";

	public CasualtyLog() { }

	public CasualtyLog(int tick, CasualtyType type, int subjectId, int sourceId, bool hostile, string prose) : base(tick) {
		Type = type;
		SubjectId = subjectId;
		SourceId = sourceId;
		Hostile = hostile;
		Prose = prose;
	}

	public override bool Concerns(int participantId) => participantId == SubjectId || participantId == SourceId;

	public override string Describe(IReadOnlyDictionary<int, ParticipantInfo> participants) {
		string subject = LabelOf(participants, SubjectId);
		return Type switch {
			CasualtyType.Downed or CasualtyType.Killed => SourceId >= 0
				? "BattleDossier.Log.Sourced".Translate(LabelOf(participants, SourceId), Prose)
				: Prose,
			CasualtyType.Captured  => "BattleDossier.Log.Captured".Translate(subject),
			CasualtyType.Fled      => "BattleDossier.Log.Fled".Translate(subject),
			CasualtyType.Destroyed => "BattleDossier.Log.Destroyed".Translate(subject),
			_                      => subject
		};
	}

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref SubjectId, "subject", -1);
		Scribe_Values.Look(ref SourceId, "source", -1);
		Scribe_Values.Look(ref Type, "type");
		Scribe_Values.Look(ref Hostile, "hostile");
		Scribe_Values.Look(ref Prose, "prose", "");
	}
}

/// <summary>A battle-level marker: start, front merge, or end.</summary>
public sealed class MarkerLog : DossierLog {
	public MarkerType Type;
	public string Text = "";

	public MarkerLog() { }

	public MarkerLog(int tick, MarkerType type, string text = "") : base(tick) {
		Type = type;
		Text = text;
	}

	public override bool Concerns(int participantId) => false;

	public override string Describe(IReadOnlyDictionary<int, ParticipantInfo> participants) => Type switch {
		MarkerType.BattleStarted => "BattleDossier.Log.Started".Translate(),
		MarkerType.FrontMerged   => "BattleDossier.Log.FrontMerged".Translate(Text),
		MarkerType.BattleEnded   => "BattleDossier.Log.Ended".Translate(),
		_                        => ""
	};

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref Type, "type");
		Scribe_Values.Look(ref Text, "text", "");
	}
}