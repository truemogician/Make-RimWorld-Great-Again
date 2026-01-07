using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.Rimfined.Components;

public sealed class NoTargetMarks : GameComponent {
	private Dictionary<int, int> _noTargetMarkExpTick = new();

	public NoTargetMarks(Game game) { }

	public bool this[Pawn pawn] {
		get {
			if (!_noTargetMarkExpTick.TryGetValue(pawn.thingIDNumber, out int expTick))
				return false;
			if (expTick <= Find.TickManager.TicksGame) {
				_noTargetMarkExpTick.Remove(pawn.thingIDNumber);
				return false;
			}
			return true;
		}
		set {
			if (value)
				Add(pawn);
			else
				Remove(pawn);
		}
	}

	public void Add(Pawn pawn, int ttl = GenDate.TicksPerDay) {
		switch (ttl) {
			case 0:    Remove(pawn); break;
			case -1:   _noTargetMarkExpTick[pawn.thingIDNumber] = -1; break;
			case < -1: throw new ArgumentOutOfRangeException(nameof(ttl), ttl, "TTL can't be less than -1");
			default:   _noTargetMarkExpTick[pawn.thingIDNumber] = Find.TickManager.TicksGame + ttl; break;
		}
	}

	public bool Remove(Pawn pawn) => _noTargetMarkExpTick.Remove(pawn.thingIDNumber);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Toggle(Pawn pawn) => this[pawn] = !this[pawn];

	public override void ExposeData() {
		if (Scribe.mode == LoadSaveMode.Saving)
			ClearExpired();
		Scribe_Collections.Look(ref _noTargetMarkExpTick, "noTargetMarkExpTick", LookMode.Value);
		if (Scribe.mode == LoadSaveMode.PostLoadInit)
			_noTargetMarkExpTick ??= new Dictionary<int, int>();
	}

	private void ClearExpired() {
		var now = Find.TickManager.TicksGame;
		var expiredIds = _noTargetMarkExpTick.Where(kvp => kvp.Value != -1 && kvp.Value <= now).Select(kvp => kvp.Key).ToArray();
		foreach (var id in expiredIds)
			_noTargetMarkExpTick.Remove(id);
	}
}