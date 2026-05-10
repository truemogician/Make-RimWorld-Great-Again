using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using TrueMogician.Extensions.Collections.Dictionary;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.WorkMemory.Components;

using WorkMemoryTuple = (int PawnId, string MemoryKey, WorkMemoryRecord Record);

public sealed class WorkMemoryComponent : GameComponent {
	private const float _RECIPE_WEIGHT = 0.7f;

	private const float _CATEGORY_WEIGHT = 0.2f;

	private const float _WORKBENCH_WEIGHT = 0.1f;

	private readonly TupleDictionary3D<int, string, WorkMemoryRecord> _records = [];

	public WorkMemoryComponent(Game game) { }

	public float GetMultiplier(Pawn pawn, WorkMemoryContext context, int delta) {
		int now = Find.TickManager.TicksGame;
		int pawnId = pawn.thingIDNumber;
		float recipeMomentum = GetMomentum(pawnId, context.Recipe.defName, now, delta);
		float referenceWorkAmount = WorkMemoryCurve.GetReferenceWorkAmount(context.Recipe);
		float categoryMomentum = context.Categories.Count == 0
			? 0f
			: context.Categories.Average(category => GetMomentum(pawnId, category.defName, now, delta));
		categoryMomentum = Mathf.Min(categoryMomentum, referenceWorkAmount);
		float workbenchMomentum = context.Workbench is null
			? 0f
			: Mathf.Min(GetMomentum(pawnId, context.Workbench.defName, now, delta), referenceWorkAmount);
		float momentum = recipeMomentum * _RECIPE_WEIGHT + categoryMomentum * _CATEGORY_WEIGHT + workbenchMomentum * _WORKBENCH_WEIGHT;
		return WorkMemoryCurve.GetMultiplier(momentum, context.Recipe);
	}

	public void RecordWork(Pawn pawn, WorkMemoryContext context, int delta) {
		delta = Mathf.Max(delta, 1);
		int pawnId = pawn.thingIDNumber;
		int now = Find.TickManager.TicksGame;
		RecordWork(pawnId, context.Recipe.defName, now, delta, delta, WorkMemoryCurve.GetMomentumCap(context.Recipe));
		if (context.Categories.Count > 0) {
			float categoryDelta = (float)delta / context.Categories.Count;
			foreach (var category in context.Categories)
				RecordWork(pawnId, category.defName, now, delta, categoryDelta, null);
		}
		if (context.Workbench is { } workbench)
			RecordWork(pawnId, workbench.defName, now, delta, delta, null);
	}

	public override void ExposeData() {
		if (Scribe.mode == LoadSaveMode.Saving)
			PruneExpired();
		var entries = Scribe.mode == LoadSaveMode.Saving ? _records.Select(tuple => (WorkMemoryEntry)tuple).ToList() : [];
		Scribe_Collections.Look(ref entries, "records", LookMode.Deep);
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
		foreach ((int pawnId, string? key) in staleKeys)
			_records.Remove(pawnId, key);
	}

	private float GetMomentum(int pawnId, string key, int now, int delta) =>
		_records.TryGetValue(pawnId, key, out var record) ? record.GetMomentum(now, delta) : 0f;

	private void RecordWork(int pawnId, string key, int now, int elapsedTicks, float momentumDelta, float? momentumCap) {
		if (!_records.TryGetValue(pawnId, key, out var record)) {
			record = new WorkMemoryRecord();
			_records[pawnId, key] = record;
		}
		record.RecordWork(now, elapsedTicks, momentumDelta, momentumCap);
	}
}

public readonly record struct WorkMemoryContext(
	RecipeDef Recipe,
	IReadOnlyList<ThingCategoryDef> Categories,
	ThingDef? Workbench
) {
	public WorkMemoryContext(Job job, RecipeDef recipe) : this(
		recipe,
		recipe.products?
			.Select(product => product.thingDef)
			.Where(def => def?.thingCategories != null)
			.SelectMany(def => def.thingCategories)
			.Distinct()
			.ToList()
		?? [],
		job.GetTarget(TargetIndex.A).Thing?.def
	) { }
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

	public bool IsExpired(int now) => _lastWorkedTick < 0 || GetMomentum(now, 0) <= 0f;

	public void RecordWork(int now, int elapsedTicks, float momentumDelta, float? momentumCap) {
		_momentum = GetMomentum(now, elapsedTicks) + momentumDelta;
		if (momentumCap is { } cap)
			_momentum = Mathf.Min(cap, _momentum);
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float GetMultiplier(float momentum, RecipeDef recipe) =>
		GetMultiplier(momentum, GetReferenceWorkAmount(recipe), MinMultiplier, MaxMultiplier);

	public static float GetMultiplier(float momentum, float referenceWorkAmount, float minMultiplier, float maxMultiplier) {
		float midpoint = referenceWorkAmount * MIDPOINT_FACTOR;
		float slope = referenceWorkAmount * SLOPE_FACTOR;
		float momentumCap = GetMomentumCap(referenceWorkAmount);
		float lowerBound = RawSigmoid(0f, midpoint, slope);
		float upperBound = RawSigmoid(momentumCap, midpoint, slope);
		float normalized = Mathf.InverseLerp(lowerBound, upperBound, RawSigmoid(Mathf.Clamp(momentum, 0f, momentumCap), midpoint, slope));
		return Mathf.Lerp(minMultiplier, maxMultiplier, normalized);
	}

	public static float GetMomentumForMultiplier(float multiplier, float referenceWorkAmount, float minMultiplier, float maxMultiplier) {
		float momentumCap = GetMomentumCap(referenceWorkAmount);
		float midpoint = referenceWorkAmount * MIDPOINT_FACTOR;
		float slope = referenceWorkAmount * SLOPE_FACTOR;
		float lowerBound = RawSigmoid(0f, midpoint, slope);
		float upperBound = RawSigmoid(momentumCap, midpoint, slope);
		float normalized = Mathf.InverseLerp(minMultiplier, maxMultiplier, multiplier);
		float raw = Mathf.Lerp(lowerBound, upperBound, normalized);
		raw = Mathf.Clamp(raw, 0.0001f, 0.9999f);
		return Mathf.Clamp(midpoint + slope * Mathf.Log(raw / (1f - raw)), 0f, momentumCap);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float GetMomentumCap(RecipeDef recipe) => GetMomentumCap(GetReferenceWorkAmount(recipe));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float GetMomentumCap(float referenceWorkAmount) => referenceWorkAmount * MOMENTUM_CAP_FACTOR;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float GetReferenceWorkAmount(float recipeWorkAmount, float warmupSpeed) {
		float amount = Mathf.Max(recipeWorkAmount, MIN_REFERENCE_WORK_AMOUNT);
		return amount / Mathf.Max(warmupSpeed, 0.01f);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float GetReferenceWorkAmount(RecipeDef recipe) => GetReferenceWorkAmount(recipe.WorkAmountTotal(null), WarmupSpeed);

	private static float RawSigmoid(float momentum, float midpoint, float slope) => 1f / (1f + Mathf.Exp(-(momentum - midpoint) / slope));
}