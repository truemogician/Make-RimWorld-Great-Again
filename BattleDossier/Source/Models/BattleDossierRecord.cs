using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.BattleDossier.Models;

/// <summary>
///     A finalized battle record, fully self-contained: it holds no references to vanilla
///     <see cref="Battle" />, <see cref="LogEntry" />, factions or maps. Identity is its <see cref="Id" />.
///     The leaderboard and timeline are both derived from <see cref="Logs" />.
/// </summary>
public class BattleDossierRecord : IExposable, ILoadReferenceable, IRenameable {
	public int Id;
	public string Name = "";

	public bool CustomName;

	public int StartTick;
	public int EndTick;
	public BattleOutcome Outcome;
	public bool Pinned;
	public List<string> MapNames = [];
	public List<ParticipantInfo> Participants = [];
	public List<DossierLog> Logs = [];

	string IRenameable.RenamableLabel {
		get => Name;
		set {
			Name = value;
			CustomName = true;
		}
	}

	string IRenameable.BaseLabel => Name;

	string IRenameable.InspectLabel => Name;

	public string GetUniqueLoadID() => $"{nameof(BattleDossierRecord)}_{Id}";

	public void ExposeData() {
		Scribe_Values.Look(ref Id, "id");
		Scribe_Values.Look(ref Name, "name", "");
		Scribe_Values.Look(ref CustomName, "customName");
		Scribe_Values.Look(ref StartTick, "startTick");
		Scribe_Values.Look(ref EndTick, "endTick");
		Scribe_Values.Look(ref Outcome, "outcome");
		Scribe_Values.Look(ref Pinned, "pinned");
		Scribe_Collections.Look(ref MapNames, "mapNames", LookMode.Value);
		Scribe_Collections.Look(ref Participants, "participants", LookMode.Deep);
		Scribe_Collections.Look(ref Logs, "logs", LookMode.Deep);
		if (Scribe.mode != LoadSaveMode.PostLoadInit)
			return;
		MapNames ??= [];
		Participants ??= [];
		Logs ??= [];
	}

	public int DurationTicks => EndTick - StartTick;

	/// <summary>Derived from <see cref="StartTick" /> on demand and cached; not persisted.</summary>
	public string BeganDate => field ??= GenDate.DateFullStringWithHourAt(GenDate.TickGameToAbs(StartTick), QuestUtility.GetLocForDates());

	public IEnumerable<string> EnemyFactionNames =>
		Participants.Where(p => p.Side == BattleSide.Enemy && !p.FactionName.NullOrEmpty()).Select(p => p.FactionName).Distinct();
}