using System.Collections.Generic;
using System.Linq;
using TrueMogician.Extensions.Collections.Dictionary;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.WorkMemory.Components;

using WorkMemoryTuple = (int PawnId, string MemoryKey, WorkMemoryRecord Record);

public sealed class WorkMemoryComponent : GameComponent {
	private const float _RECIPE_WEIGHT = 0.4f;

	private const float _PRODUCT_WEIGHT = 0.3f;

	private const float _CATEGORY_WEIGHT = 0.2f;

	private const float _WORKBENCH_WEIGHT = 0.1f;

	private readonly TupleDictionary3D<int, string, WorkMemoryRecord> _records = [];

	public WorkMemoryComponent(Game game) { }

	public float GetMultiplier(Pawn pawn, WorkMemoryContext context, int delta) {
		int now = Find.TickManager.TicksGame;
		int pawnId = pawn.thingIDNumber;
		float workAmount = WorkMemoryCurve.GetReferenceWorkAmount(context.Recipe);
		float momentum = _RECIPE_WEIGHT * GetMomentum(pawnId, context.Recipe.defName, now, delta, workAmount);
		float totalWeight = _RECIPE_WEIGHT;
		if (context.Product is { } product) {
			momentum += _PRODUCT_WEIGHT * Mathf.Min(GetMomentum(pawnId, product.defName, now, delta, workAmount), workAmount);
			totalWeight += _PRODUCT_WEIGHT;
		}
		if (context.Categories.Count > 0) {
			float avg = context.Categories.Average(category => GetMomentum(pawnId, category.defName, now, delta, workAmount));
			momentum += _CATEGORY_WEIGHT * Mathf.Min(avg, workAmount);
			totalWeight += _CATEGORY_WEIGHT;
		}
		if (context.Workbench is { } workbench) {
			momentum += _WORKBENCH_WEIGHT * Mathf.Min(GetMomentum(pawnId, workbench.defName, now, delta, workAmount), workAmount);
			totalWeight += _WORKBENCH_WEIGHT;
		}
		return WorkMemoryCurve.GetMultiplier(momentum / totalWeight, context.Recipe);
	}

	public void RecordWork(Pawn pawn, WorkMemoryContext context, int delta) {
		delta = Mathf.Max(delta, 1);
		int pawnId = pawn.thingIDNumber;
		int now = Find.TickManager.TicksGame;
		RecordWork(pawnId, context.Recipe.defName, now, delta, delta, WorkMemoryCurve.GetMomentumCap(context.Recipe));
		if (context.Product is { } product)
			RecordWork(pawnId, product.defName, now, delta, delta, null);
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

	private float GetMomentum(int pawnId, string key, int now, int delta, float referenceWorkAmount) =>
		_records.TryGetValue(pawnId, key, out var record) ? record.GetMomentum(now, delta, referenceWorkAmount) : 0f;

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
	ThingDef? Product,
	IReadOnlyList<ThingCategoryDef> Categories,
	ThingDef? Workbench
) {
	public WorkMemoryContext(Job job, RecipeDef recipe) : this(
		recipe,
		recipe.products?.Select(product => product.thingDef).FirstOrDefault(def => def != null),
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

	private float _cumulativeWork;

	public void ExposeData() {
		Scribe_Values.Look(ref _lastWorkedTick, "lastWorkedTick", -1);
		Scribe_Values.Look(ref _momentum, "momentum");
		Scribe_Values.Look(ref _cumulativeWork, "cumulativeWork");
	}

	public float GetMomentum(int now, int delta, float referenceWorkAmount)
		=> Mathf.Max(GetDecayingMomentum(now, delta), WorkMemoryCurve.GetPermanentMomentum(_cumulativeWork, referenceWorkAmount));

	public bool IsExpired(int now) => _lastWorkedTick < 0 || (_cumulativeWork <= 0f && GetDecayingMomentum(now, 0) <= 0f);

	public void RecordWork(int now, int elapsedTicks, float momentumDelta, float? momentumCap) {
		_momentum = GetDecayingMomentum(now, elapsedTicks) + momentumDelta;
		if (momentumCap is { } cap)
			_momentum = Mathf.Min(cap, _momentum);
		_cumulativeWork += momentumDelta;
		_lastWorkedTick = now;
	}

	private float GetDecayingMomentum(int now, int delta) {
		int gap = _lastWorkedTick < 0
			? int.MaxValue
			: Mathf.Max(0, now - _lastWorkedTick - delta - WorkMemoryCurve.DecayDelay);
		return Mathf.Max(0f, _momentum - gap * WorkMemoryCurve.DecayPerTick);
	}
}