using System.Linq;
using TrueMogician.Extensions.Collections.Dictionary;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable.Components;

using WorkMemoryTuple = (int PawnId, string RecipeDefName, WorkMemoryRecord Record);

public sealed class WorkMemoryComponent : GameComponent {
	private readonly TupleDictionary3D<int, string, WorkMemoryRecord> _records = new();

	public WorkMemoryComponent(Game game) { }

	public float GetMultiplier(Pawn pawn, RecipeDef recipe, int delta) {
		return !_records.TryGetValue(pawn.thingIDNumber, recipe.defName, out var record) 
			? WorkMemoryCurve.MIN_MULTIPLIER 
			: WorkMemoryCurve.GetMultiplier(record.GetMomentum(Find.TickManager.TicksGame, delta));
	}

	public void RecordWork(Pawn pawn, RecipeDef recipe, int delta) {
		delta = Mathf.Max(delta, 1);
		int pawnId = pawn.thingIDNumber;
		string recipeDefName = recipe.defName;
		if (!_records.TryGetValue(pawnId, recipeDefName, out var record)) {
			record = new WorkMemoryRecord();
			_records[pawnId, recipeDefName] = record;
		}
		record.RecordWork(Find.TickManager.TicksGame, delta);
	}

	public override void ExposeData() {
		if (Scribe.mode == LoadSaveMode.Saving)
			PruneExpired();
		var entries = Scribe.mode == LoadSaveMode.Saving ? _records.Select(tuple => (WorkMemoryEntry)tuple).ToList() : [];
		Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
		if (Scribe.mode == LoadSaveMode.LoadingVars) {
			_records.Clear();
			if (entries == null)
				return;
			foreach (var entry in entries) {
				if (entry is not { PawnId: > 0, RecipeDefName: not null, Record: not null })
					continue;
				_records.Add(entry); 
			}
		}
		else if (Scribe.mode == LoadSaveMode.PostLoadInit)
			PruneExpired();
	}

	private void PruneExpired() {
		if (_records.Count == 0)
			return;
		int now = Find.TickManager.TicksGame;
		var staleKeys = _records
			.Where(tuple => tuple.Item3 == null || tuple.Item3.IsExpired(now))
			.Select(tuple => (tuple.Item1, tuple.Item2))
			.ToArray();
		foreach (var (pawnId, recipeDefName) in staleKeys)
			_records.Remove(pawnId, recipeDefName);
	}
}

public sealed class WorkMemoryEntry : IExposable {
	public int PawnId;

	public string? RecipeDefName;

	public WorkMemoryRecord? Record;

	public void ExposeData() {
		Scribe_Values.Look(ref PawnId, "pawnId");
		Scribe_Values.Look(ref RecipeDefName, "recipeDefName");
		Scribe_Deep.Look(ref Record, "record");
	}

	public static implicit operator WorkMemoryTuple(WorkMemoryEntry entry) => (entry.PawnId, entry.RecipeDefName ?? string.Empty, entry.Record ?? new()); 

	public static implicit operator WorkMemoryEntry(WorkMemoryTuple tuple) => new() {
		PawnId = tuple.PawnId, 
		RecipeDefName = tuple.RecipeDefName, 
		Record = tuple.Record
	};

}

public sealed class WorkMemoryRecord : IExposable {
	private int _lastWorkedTick = -1;

	private float _momentum;

	public void ExposeData() {
		Scribe_Values.Look(ref _lastWorkedTick, "lastWorkedTick", -1);
		Scribe_Values.Look(ref _momentum, "momentum", 0f);
	}

	public float GetMomentum(int now, int delta) {
		int gap = _lastWorkedTick < 0 ? int.MaxValue : Mathf.Max(0, now - _lastWorkedTick - delta);
		return Mathf.Max(0f, _momentum - gap * WorkMemoryCurve.DECAY_PER_TICK);
	}

	public bool IsExpired(int now) {
		if (_lastWorkedTick < 0)
			return true;
		return GetMomentum(now, 0) <= 0f;
	}

	public void RecordWork(int now, int delta) {
		_momentum = Mathf.Min(WorkMemoryCurve.MOMENTUM_CAP, GetMomentum(now, delta) + delta);
		_lastWorkedTick = now;
	}
}

internal static class WorkMemoryCurve {
	public const float MIN_MULTIPLIER = 0.5f;

	public const float MAX_MULTIPLIER = 1.25f;

	public const float MIDPOINT = 600f;

	public const float SLOPE = 150f;

	public const float MOMENTUM_CAP = 1200f;

	public const float DECAY_PER_TICK = 1f;

	private static readonly float _lowerBound = RawSigmoid(0f);

	private static readonly float _upperBound = RawSigmoid(MOMENTUM_CAP);

	public static float GetMultiplier(float momentum) {
		float normalized = Mathf.InverseLerp(_lowerBound, _upperBound, RawSigmoid(Mathf.Clamp(momentum, 0f, MOMENTUM_CAP)));
		return Mathf.Lerp(MIN_MULTIPLIER, MAX_MULTIPLIER, normalized);
	}

	private static float RawSigmoid(float momentum) => 1f / (1f + Mathf.Exp(-(momentum - MIDPOINT) / SLOPE));
}
