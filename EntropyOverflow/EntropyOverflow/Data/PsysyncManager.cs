using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace TrueMogician.RimWorld.EntropyOverflow.Data;

// Owns and persists all ResonanceGroups; provides helpers for dev/debug and gameplay systems.
public class PsysyncManager : GameComponent {
	// Rebuilt on load; not persisted directly to avoid fragile keys.
	private readonly Dictionary<Pawn, ResonanceGroup> _membership = new();

	private Dictionary<int, ResonanceGroup> _groups = new();

	public PsysyncManager(Game game) { }

	public int GroupCount => _groups.Count;

	public ICollection<ResonanceGroup> Groups => _groups.Values;

	public ResonanceGroup this[int id] => _groups[id];

	public ResonanceGroup this[Pawn pawn] => _membership[pawn];

	public override void GameComponentOnGUI() {
		/* reserved for HUD widgets later */
	}

	public override void FinalizeInit() => RebuildMembershipIndex();

	public override void ExposeData() {
		var groups = _groups.Values.ToList();
		Scribe_Collections.Look(ref _groups, "groups", LookMode.Deep);
		if (Scribe.mode == LoadSaveMode.LoadingVars) {
			_groups.Clear();
			groups.ForEach(g => _groups.Add(g.Id, g));
		}
		if (Scribe.mode == LoadSaveMode.PostLoadInit)
			RebuildMembershipIndex();
	}

	public ResonanceGroup CreateGroup(int resonatorId, IEnumerable<Pawn> pawns, int tick = -1) {
		var group = new ResonanceGroup(GroupCount + 1, resonatorId, pawns, tick);
		IndexGroup(group);
		_groups[group.Id] = group;
		return group;
	}

	public bool TryGetPawnGroup(Pawn pawn, out ResonanceGroup? group) 
		=> _membership.TryGetValue(pawn, out group);

	private void RebuildMembershipIndex() {
		_membership.Clear();
		foreach (var g in _groups.Values)
			IndexGroup(g);
	}

	private void IndexGroup(ResonanceGroup g) {
		foreach (var p in g.Members) {
			if (_membership.TryGetValue(p, out var existing))
				throw new InvalidOperationException($"Pawn {p.Name} is already a member of resonance group {existing.Id}.");
			_membership[p] = g;
		}
	}
}