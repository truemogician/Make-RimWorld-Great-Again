using System.Linq;
using RimWorld;
using TrueMogician.Extensions.Collections.Dictionary;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.WorkMemory.Components;

using WorkMemoryTuple = (int PawnId, string MemoryKey, WorkMemoryRecord Record);

public sealed class WorkMemoryComponent : GameComponent {
	private readonly TupleDictionary3D<int, string, WorkMemoryRecord> _records = [];

	public WorkMemoryComponent(Game game) { }

	public float GetMultiplier(Pawn pawn, string memoryKey, RecipeDef recipe, int delta) {
		return !_records.TryGetValue(pawn.thingIDNumber, memoryKey, out var record)
			? WorkMemoryCurve.MinMultiplier
			: WorkMemoryCurve.GetMultiplier(record.GetMomentum(Find.TickManager.TicksGame, delta), recipe);
	}

	public void RecordWork(Pawn pawn, string memoryKey, RecipeDef recipe, int delta) {
		delta = Mathf.Max(delta, 1);
		int pawnId = pawn.thingIDNumber;
		if (!_records.TryGetValue(pawnId, memoryKey, out var record)) {
			record = new WorkMemoryRecord();
			_records[pawnId, memoryKey] = record;
		}
		record.RecordWork(Find.TickManager.TicksGame, delta, WorkMemoryCurve.GetMomentumCap(recipe));
	}

	public void ClearRecords() => _records.Clear();

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
				if (entry is not { PawnId: > 0, MemoryKey: not null, Record: not null })
					continue;
				_records.Add(entry);
			}
		}
		else if (Scribe.mode == LoadSaveMode.PostLoadInit) {
			PruneExpired();
		}
	}

	private void PruneExpired() {
		if (_records.Count == 0)
			return;
		int now = Find.TickManager.TicksGame;
		var staleKeys = _records
			.Where(tuple => tuple.Item3 == null || tuple.Item3.IsExpired(now))
			.Select(tuple => (tuple.Item1, tuple.Item2))
			.ToArray();
		foreach ((int pawnId, string? memoryKey) in staleKeys)
			_records.Remove(pawnId, memoryKey);
	}
}

public sealed class WorkMemoryEntry : IExposable {
	public int PawnId;

	public string? MemoryKey;

	public WorkMemoryRecord? Record;

	public void ExposeData() {
		Scribe_Values.Look(ref PawnId, "pawnId");
		Scribe_Values.Look(ref MemoryKey, "memoryKey");
		Scribe_Deep.Look(ref Record, "record");
	}

	public static implicit operator WorkMemoryTuple(WorkMemoryEntry entry) => (entry.PawnId, entry.MemoryKey ?? string.Empty, entry.Record ?? new());

	public static implicit operator WorkMemoryEntry(WorkMemoryTuple tuple) => new() {
		PawnId = tuple.PawnId,
		MemoryKey = tuple.MemoryKey,
		Record = tuple.Record
	};
}

public sealed class WorkMemoryRecord : IExposable {
	private int _lastWorkedTick = -1;

	private float _momentum;

	public void ExposeData() {
		Scribe_Values.Look(ref _lastWorkedTick, "lastWorkedTick", -1);
		Scribe_Values.Look(ref _momentum, "momentum");
	}

	public float GetMomentum(int now, int delta) {
		int gap = _lastWorkedTick < 0
			? int.MaxValue
			: Mathf.Max(0, now - _lastWorkedTick - delta - WorkMemoryCurve.DecayDelay);
		return Mathf.Max(0f, _momentum - gap * WorkMemoryCurve.DecayPerTick);
	}

	public bool IsExpired(int now) {
		if (_lastWorkedTick < 0)
			return true;
		return GetMomentum(now, 0) <= 0f;
	}

	public void RecordWork(int now, int deltaTicks, float momentumCap) {
		_momentum = Mathf.Min(momentumCap, GetMomentum(now, deltaTicks) + deltaTicks);
		_lastWorkedTick = now;
	}
}

public static class WorkMemoryCurve {
	public const float DEFAULT_PENALTY = 0.3f;

	public const float DEFAULT_WARMUP_SPEED = 1f;

	public const float MIN_REFERENCE_WORK_AMOUNT = 200f;

	public const float MIDPOINT_FACTOR = 1f;

	public const float SLOPE_FACTOR = 0.2f;

	public const float MOMENTUM_CAP_FACTOR = 2f;

	public const int DEFAULT_DECAY_DELAY = 1 * GenDate.TicksPerDay;

	public const float DEFAULT_DECAY_SPEED = 0.25f;

	public static float MinMultiplier => Settings.Default is { } settings ? settings.MinMultiplier : 1f - DEFAULT_PENALTY;

	public static float MaxMultiplier => Settings.Default is { } settings ? settings.MaxMultiplier : 1f + DEFAULT_PENALTY * 0.5f;

	public static float WarmupSpeed => Settings.Default is { } settings ? settings.WarmupSpeed : DEFAULT_WARMUP_SPEED;

	public static int DecayDelay => Settings.Default is { } settings ? settings.DecayDelay : DEFAULT_DECAY_DELAY;

	public static float DecayPerTick => Mathf.Max(0f, Settings.Default is { } settings ? settings.DecaySpeed : DEFAULT_DECAY_SPEED);

	public static float GetMomentumCap(RecipeDef recipe) => GetReferenceWorkAmount(recipe) * MOMENTUM_CAP_FACTOR;

	public static float GetMultiplier(float momentum, RecipeDef recipe) {
		var workAmount = GetReferenceWorkAmount(recipe);
		float midpoint = workAmount * MIDPOINT_FACTOR;
		float slope = workAmount * SLOPE_FACTOR;
		float momentumCap = workAmount * MOMENTUM_CAP_FACTOR;
		float lowerBound = RawSigmoid(0f, midpoint, slope);
		float upperBound = RawSigmoid(momentumCap, midpoint, slope);
		float normalized = Mathf.InverseLerp(lowerBound, upperBound, RawSigmoid(Mathf.Clamp(momentum, 0f, momentumCap), midpoint, slope));
		return Mathf.Lerp(MinMultiplier, MaxMultiplier, normalized);
	}

	private static float GetReferenceWorkAmount(RecipeDef recipe) {
		float amount = Mathf.Max(recipe.WorkAmountTotal(null), MIN_REFERENCE_WORK_AMOUNT);
		return amount / Mathf.Max(WarmupSpeed, 0.01f);
	}

	private static float RawSigmoid(float momentum, float midpoint, float slope) => 1f / (1f + Mathf.Exp(-(momentum - midpoint) / slope));
}