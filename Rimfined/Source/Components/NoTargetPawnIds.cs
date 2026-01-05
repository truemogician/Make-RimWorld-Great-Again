using System;
using System.Collections.Generic;
using Verse;

namespace TrueMogician.RimWorld.Rimfined.Components;

public sealed class NoTargetPawnIds : GameComponent {
	private HashSet<string> _noTargetPawnIds = new(StringComparer.Ordinal);

	public NoTargetPawnIds(Game game) { }

	public bool this[Pawn pawn] {
		get => _noTargetPawnIds.Contains(pawn.GetUniqueLoadID());
		set {
			string? id = pawn.GetUniqueLoadID();
			if (value)
				_noTargetPawnIds.Add(id);
			else
				_noTargetPawnIds.Remove(id);
		}
	}

	public void Toggle(Pawn pawn) {
		string? id = pawn.GetUniqueLoadID();
		if (!_noTargetPawnIds.Add(id))
			_noTargetPawnIds.Remove(id);
	}

	public override void ExposeData() {
		Scribe_Collections.Look(ref _noTargetPawnIds, "noTargetPawnIds", LookMode.Value);
		if (Scribe.mode == LoadSaveMode.PostLoadInit)
			_noTargetPawnIds ??= new HashSet<string>(StringComparer.Ordinal);
	}
}