using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.ExactStorage;

/// <summary>
///     Per-map index of ES-derived state. Centralizes invalidation: profile-version-keyed fields
///     refresh only when ES profiles/quotas mutate; tick-keyed fields refresh once per game tick.
/// </summary>
internal sealed class MapIndex(Map map) {
	private long _activeDefsVersion = -1;

	private readonly HashSet<string> _activeDefs = [];

	private int _activeJobsTick = -1;

	private readonly List<(Pawn Pawn, Job Job)> _activeJobs = [];

	public IReadOnlyList<(Pawn Pawn, Job Job)> ActiveJobs {
		get {
			EnsureActiveJobs();
			return _activeJobs;
		}
	}

	public bool IsActiveFor(ThingDef def) {
		EnsureActiveDefs();
		return _activeDefs.Contains(def.defName);
	}

	private void EnsureActiveDefs() {
		if (_activeDefsVersion == Manager.ProfileVersion)
			return;
		_activeDefs.Clear();
		foreach (var group in map.haulDestinationManager.AllGroupsListInPriorityOrder) {
			var settings = group?.parent?.GetStoreSettings();
			if (settings is null)
				continue;
			if (!Manager.TryGetProfile(settings, out var profile) || !profile.Enabled)
				continue;
			foreach (var quota in profile.Quotas) {
				if (!profile.QuotaValid(quota))
					continue;
				switch (quota) {
					case ThingQuota { ThingDef: { } td }: _activeDefs.Add(td.defName); break;
					case ThingCategoryQuota { CategoryDef: { } cd }:
						foreach (var d in DefCache.DescendantThingsOf(cd))
							_activeDefs.Add(d.defName);
						break;
				}
			}
		}
		_activeDefsVersion = Manager.ProfileVersion;
	}

	private void EnsureActiveJobs() {
		int tick = Find.TickManager?.TicksGame ?? -1;
		if (_activeJobsTick == tick && tick >= 0)
			return;
		_activeJobsTick = tick;
		_activeJobs.Clear();
		var seen = new HashSet<Job>();
		foreach (var pawn in map.mapPawns.AllPawnsSpawned) {
			if (pawn.jobs is null)
				continue;
			foreach (var job in pawn.jobs.AllJobs()) {
				if (job is null || !seen.Add(job))
					continue;
				_activeJobs.Add((pawn, job));
			}
		}
		foreach (var reservation in map.reservationManager.ReservationsReadOnly) {
			var job = reservation.Job;
			if (job is null || !seen.Add(job))
				continue;
			_activeJobs.Add((reservation.Claimant, job));
		}
	}
}

internal static class MapIndexRegistry {
	private static readonly ConditionalWeakTable<Map, MapIndex> _indexes = new();

	public static MapIndex For(Map map) => _indexes.GetValue(map, static m => new MapIndex(m));
}