using System.Collections.Generic;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.RemedialAlgorithms.Components;

// Maintains 3 lists in lockstep so Tend WorkGivers don't pay an O(N_pawns) rebuild + downstream
// per-target filtering every think tree pass:
//   • WithAnyHediff           — replaces vanilla MapPawns.SpawnedPawnsWithAnyHediff
//   • NeedingTending          — narrow target list for the regular Tend workgivers
//   • NeedingUrgentTending    — narrow target list for the *Urgent / *Emergency variants
public sealed class TendTargetCache(Map map) : MapComponent(map) {
	private readonly HashSet<Pawn> _withAnyHediffSet = [];

	private readonly List<Pawn> _withAnyHediff = [];

	private readonly HashSet<Pawn> _needingTendingSet = [];

	private readonly List<Pawn> _needingTending = [];

	private readonly HashSet<Pawn> _needingUrgentTendingSet = [];

	private readonly List<Pawn> _needingUrgentTending = [];

	private bool _initialized;

	private bool _subscribed;

	public List<Pawn> WithAnyHediff {
		get {
			EnsureInitialized();
			return _withAnyHediff;
		}
	}

	public List<Pawn> NeedingTending {
		get {
			EnsureInitialized();
			return _needingTending;
		}
	}

	public List<Pawn> NeedingUrgentTending {
		get {
			EnsureInitialized();
			return _needingUrgentTending;
		}
	}

	public override void FinalizeInit() {
		base.FinalizeInit();
		Subscribe();
		Rebuild();
	}

	public override void MapRemoved() {
		Unsubscribe();
		base.MapRemoved();
	}

	internal void Subscribe() {
		if (_subscribed)
			return;
		HediffDirtyHub.PawnHediffsDirtied += OnHediffsDirtied;
		HediffDirtyHub.PawnSpawnedOnMap += OnPawnSpawned;
		HediffDirtyHub.PawnDespawnedFromMap += OnPawnDespawned;
		_subscribed = true;
	}

	internal void Unsubscribe() {
		if (!_subscribed)
			return;
		HediffDirtyHub.PawnHediffsDirtied -= OnHediffsDirtied;
		HediffDirtyHub.PawnSpawnedOnMap -= OnPawnSpawned;
		HediffDirtyHub.PawnDespawnedFromMap -= OnPawnDespawned;
		_subscribed = false;
	}

	internal void Rebuild() {
		_withAnyHediff.Clear();
		_withAnyHediffSet.Clear();
		_needingTending.Clear();
		_needingTendingSet.Clear();
		_needingUrgentTending.Clear();
		_needingUrgentTendingSet.Clear();
		foreach (var pawn in map.mapPawns.AllPawnsSpawned)
			Reevaluate(pawn);
		_initialized = true;
	}

	private static void UpdateMembership(List<Pawn> list, HashSet<Pawn> set, Pawn pawn, bool shouldBeMember) {
		if (shouldBeMember) {
			if (set.Add(pawn))
				list.Add(pawn);
		}
		else if (set.Remove(pawn))
			list.Remove(pawn);
	}

	private void EnsureInitialized() {
		if (!_initialized)
			Rebuild();
	}

	private void OnHediffsDirtied(Pawn pawn) {
		if (pawn.Map != map)
			return;
		Reevaluate(pawn);
	}

	private void OnPawnSpawned(Pawn pawn, Map other) {
		if (other != map)
			return;
		Reevaluate(pawn);
	}

	private void OnPawnDespawned(Pawn pawn, Map other) {
		if (other != map)
			return;
		Remove(pawn);
	}

	private void Reevaluate(Pawn pawn) {
		if (pawn.health?.hediffSet is not { } hediffSet) {
			Remove(pawn);
			return;
		}
		bool anyHediff = hediffSet.hediffs.Count > 0;
		UpdateMembership(_withAnyHediff, _withAnyHediffSet, pawn, anyHediff);
		bool needsTending = anyHediff && HealthAIUtility.ShouldBeTendedNowByPlayer(pawn);
		UpdateMembership(_needingTending, _needingTendingSet, pawn, needsTending);
		bool needsUrgent = needsTending && HealthAIUtility.ShouldBeTendedNowByPlayerUrgent(pawn);
		UpdateMembership(_needingUrgentTending, _needingUrgentTendingSet, pawn, needsUrgent);
	}

	private void Remove(Pawn pawn) {
		UpdateMembership(_withAnyHediff, _withAnyHediffSet, pawn, false);
		UpdateMembership(_needingTending, _needingTendingSet, pawn, false);
		UpdateMembership(_needingUrgentTending, _needingUrgentTendingSet, pawn, false);
	}
}