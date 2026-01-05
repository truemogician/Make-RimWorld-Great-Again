using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.Rimfined.Components;

public sealed class PawnsToCapture(Map map) : MapComponent(map), IEnumerable<Pawn> {
	private readonly HashSet<Pawn> _marked = [];

	public IEnumerator<Pawn> GetEnumerator() => _marked.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public bool AnyMarked => _marked.Count > 0;

	public bool this[Pawn pawn] {
		get => _marked.Contains(pawn);
		set {
			if (!value)
				_marked.Remove(pawn);
			else if (ValidForCapture(pawn))
				_marked.Add(pawn);
		}
	}

	public static bool ValidForCapture(Pawn pawn)
		=> pawn.CanBeCaptured() && (!pawn.IsPrisonerOfColony || !pawn.Position.IsInPrisonCell(pawn.MapHeld));

	public void Toggle(Pawn pawn) {
		if (!_marked.Add(pawn))
			_marked.Remove(pawn);
	}

	public override void MapComponentTick() {
		if (map.IsHashIntervalTick(250))
			return;
		_marked.RemoveWhere(p => !ValidForCapture(p));
	}

	public override void ExposeData() {
		base.ExposeData();
		List<Pawn> markedPawns = [];
		if (Scribe.mode == LoadSaveMode.Saving)
			markedPawns = _marked.Where(ValidForCapture).ToList();
		Scribe_Collections.Look(ref markedPawns, "markedForCapture", LookMode.Reference);
		if (Scribe.mode == LoadSaveMode.PostLoadInit) {
			_marked.Clear();
			if (markedPawns is { Count: > 0 }) {
				foreach (var p in markedPawns) {
					if (p is not null)
						_marked.Add(p);
				}
			}
		}
	}
}