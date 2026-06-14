using System.Collections.Generic;
using TrueMogician.Exceptions;
using TrueMogician.RimWorld.BattleDossier.Models;

namespace TrueMogician.RimWorld.BattleDossier.Core;

/// <summary>Per-participant aggregates derived from the log.</summary>
public sealed class ParticipantSummary {
	public float DamageDealt;
	public float FriendlyFire;
	public float DamageTaken;
	public int Kills;
	public int Downs;
	public ParticipantFate Fate = ParticipantFate.Intact;
}

/// <summary>Everything the leaderboard, overview and fate column need, computed in one pass over the logs.</summary>
public sealed class BattleStatsResult {
	private readonly Dictionary<int, ParticipantSummary> _summaries = [];

	/// <summary>
	///     Create a new result by processing the record list.
	/// </summary>
	/// <param name="records">A chronological list of records. </param>
	internal BattleStatsResult(IReadOnlyList<DossierLog> records, IReadOnlyDictionary<int, ParticipantInfo> participants) {
		foreach (int id in participants.Keys)
			_summaries[id] = new ParticipantSummary();
		foreach (var rec in records) {
			switch (rec) {
				case HitLog hit: {
					TotalDamage += hit.Damage;
					if (hit.SubjectId >= 0) {
						var subject = GetOrCreate(hit.SubjectId);
						subject.DamageTaken += hit.Damage;
						if (subject.Fate == ParticipantFate.Intact)
							subject.Fate = ParticipantFate.Hit;
					}
					if (hit.InstigatorId < 0)
						UnattributedDamage += hit.Damage;
					else if (hit.InstigatorId != hit.SubjectId) {
						var instigator = GetOrCreate(hit.InstigatorId);
						if (hit.Hostile)
							instigator.DamageDealt += hit.Damage;
						else
							instigator.FriendlyFire += hit.Damage;
					}
					break;
				}
				case CasualtyLog casualty: {
					var subject = GetOrCreate(casualty.SubjectId);
					var fate = CasualtyToFate(casualty.Type);
					if (FateRank(fate) > FateRank(subject.Fate))
						subject.Fate = fate;
					if (casualty is { Hostile: true, SourceId: >= 0 }) {
						switch (casualty.Type) {
							case CasualtyType.Killed: GetOrCreate(casualty.SourceId).Kills++; break;
							case CasualtyType.Downed: GetOrCreate(casualty.SourceId).Downs++; break;
						}
					}
					break;
				}
			}
		}
	}

	public float TotalDamage { get; }

	public float UnattributedDamage { get; }

	public IReadOnlyDictionary<int, ParticipantSummary> Summaries => _summaries;

	public static ParticipantFate CasualtyToFate(CasualtyType type) => type switch {
		CasualtyType.Downed                           => ParticipantFate.Downed,
		CasualtyType.Killed or CasualtyType.Destroyed => ParticipantFate.Dead,
		CasualtyType.Fled                             => ParticipantFate.Fled,
		CasualtyType.Captured                         => ParticipantFate.Captured,
		_                                             => throw new EnumValueOutOfRangeException(typeof(CasualtyType), type)
	};

	// Higher rank wins when a subject has several casualty logs (e.g. Downed then Killed -> Dead).
	private static int FateRank(ParticipantFate fate) => fate switch {
		ParticipantFate.Intact   => 0,
		ParticipantFate.Hit      => 1,
		ParticipantFate.Downed   => 2,
		ParticipantFate.Dead     => 3,
		ParticipantFate.Fled     => 4,
		ParticipantFate.Captured => 4,
		_                        => -1
	};

	private ParticipantSummary GetOrCreate(int id) => _summaries.TryGetValue(id, out var t) ? t : _summaries[id] = new ParticipantSummary();
}