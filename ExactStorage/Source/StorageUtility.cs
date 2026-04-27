using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.ExactStorage;

public static class StorageUtility {
	public const int NO_LIMIT = int.MaxValue;

	private static readonly List<EnrouteStockProvider> _enrouteStockProviders = [];

	public delegate decimal EnrouteStockProvider(StorageSettings settings, Quota quota, ISlotGroupParent? parent, Map map, Pawn pawn, Job job);

	private enum CellSearchMode {
		PreferMinimum,
		AnyAllowed
	}

	public static bool SupportsExactStorage(StorageSettings? settings)
		=> settings?.owner is StorageGroup or ISlotGroupParent;

	public static bool UseSeparateLinkedStorage(StorageSettings settings)
		=> Manager.TryGetProfile(settings, out var profile)
			&& profile is { Enabled: true, SeparateLinkedStorages: true }
			&& SeparateLinkedStorageAvailable(settings);

	public static bool SeparateLinkedStorageAvailable(StorageSettings settings) {
		if (settings.owner is not StorageGroup { MemberCount: > 1 } group)
			return false;
		ThingDef? def = null;
		foreach (var member in group.members) {
			if (member is not Building_Storage building)
				return false;
			if (def is null) {
				def = building.def;
				continue;
			}
			if (def != building.def)
				return false;
		}
		return def is not null;
	}

	public static IEnumerable<Thing> HeldThings(StorageSettings? settings, ISlotGroupParent? parent = null) {
		if (parent?.GetSlotGroup() is { } scopedGroup) {
			foreach (var thing in scopedGroup.HeldThings)
				yield return thing;
			yield break;
		}
		switch (settings?.owner) {
			case StorageGroup group: {
				foreach (var thing in group.HeldThings)
					yield return thing;
				yield break;
			}
			case ISlotGroupParent settingsParent when settingsParent.GetSlotGroup() is { } slotGroup: {
				foreach (var thing in slotGroup.HeldThings)
					yield return thing;
				break;
			}
		}
	}

	public static bool Contains(StorageSettings? settings, Thing thing) => Contains(settings, thing, null);

	public static bool Allows(StorageSettings settings, Thing thing, bool currentlyStored, ISlotGroupParent? parent = null)
		=> Allows(settings, thing, currentlyStored, parent, null);

	public static bool ShouldPreferForMinimum(StorageSettings settings, Thing thing, IntVec3? storeCell = null, Map? map = null)
		=> ShouldPreferForMinimum(settings, thing, storeCell, map, (Job?)null);

	public static bool ShouldPreferForMinimum(StorageSettings settings, Thing thing, IntVec3? storeCell, Map? map, Job? ignoredJob)
		=> ShouldPreferForMinimum(settings, thing, storeCell, map, new StorageEvaluationCache(ignoredJob));

	public static int DestinationCountLimit(StorageSettings settings, Thing thing, bool preferMinimum, IntVec3 storeCell, Map map)
		=> DestinationCountLimit(settings, thing, preferMinimum, storeCell, map, (Job?)null);

	public static int DestinationCountLimit(StorageSettings settings, Thing thing, bool preferMinimum, IntVec3 storeCell, Map map, Job? ignoredJob)
		=> DestinationCountLimit(settings, thing, preferMinimum, storeCell, map, new StorageEvaluationCache(ignoredJob));

	public static void AddEnrouteStockProvider(EnrouteStockProvider provider) {
		if (!_enrouteStockProviders.Contains(provider))
			_enrouteStockProviders.Add(provider);
	}

	public static int SourceExcessLimit(Thing thing) => SourceExcessLimit(thing, null);

	public static bool CanReceiveAt(IntVec3 cell, Map map, Thing thing) => CanReceiveAt(cell, map, thing, null);

	public static bool TryGetCapacity(StorageSettings settings, out int capacity)
		=> TryGetCapacity(settings, ParentForCell(settings, null, null), out capacity);

	public static void NotifyChanged(StorageSettings? settings) {
		settings?.owner?.Notify_SettingsChanged();
	}

	public static bool MatchesScope(StorageSettings settings, ISlotGroupParent? parent, Map map, LocalTargetInfo target) {
		if (target.HasThing || !target.Cell.IsValid)
			return false;
		var slotGroup = target.Cell.GetSlotGroup(map);
		if (slotGroup is null)
			return false;
		if (parent is not null)
			return slotGroup.parent == parent;
		return settings.owner switch {
			StorageGroup group              => slotGroup.StorageGroup == group,
			ISlotGroupParent settingsParent => slotGroup.parent == settingsParent,
			_                               => false
		};
	}

	internal static bool Contains(StorageSettings? settings, Thing thing, StorageEvaluationCache? evaluation) {
		if (evaluation is not null)
			return evaluation.Contains(settings, thing);
		var parent = ParentForStoredThing(settings, thing);
		foreach (var heldThing in HeldThings(settings, parent)) {
			if (heldThing == thing)
				return true;
		}
		return false;
	}

	internal static bool Allows(
		StorageSettings settings,
		Thing thing,
		bool currentlyStored,
		ISlotGroupParent? parent,
		StorageEvaluationCache? evaluation
	) {
		if (!SupportsExactStorage(settings) || !Manager.TryGetProfile(settings, out var profile) || !profile.Enabled)
			return true;
		if (parent is null && currentlyStored)
			parent = ParentForStoredThing(settings, thing);
		if (UseSeparateLinkedStorage(settings) && parent is null && !currentlyStored)
			return true;
		var quotas = evaluation?.MatchingQuotas(profile, thing) ?? profile.MatchingQuotas(thing);
		foreach (var quota in quotas) {
			if (QuotaAllowed(settings, quota) && quota.HasMax && profile.CountFor(quota, parent, evaluation) > quota.MaxStock)
				return false;
		}
		var balance = MinimumBalance.For(settings, profile, parent, evaluation);
		if (currentlyStored)
			return !balance.ShouldDisplace(thing);
		if (!balance.CanAccept(thing, null, null))
			return false;
		if (quotas.Count == 0)
			return true;
		if (!RefillGate.AllowsRefill(settings))
			return false;
		foreach (var quota in quotas) {
			if (QuotaAllowed(settings, quota) && quota.HasMax && profile.CountFor(quota, parent, evaluation) >= quota.MaxStock)
				return false;
		}
		return true;
	}

	internal static bool QuotaAllowed(StorageSettings settings, Quota quota) {
		if (quota.ThingDef is { } thingDef)
			return settings.filter.Allows(thingDef);
		if (quota.CategoryDef is { } categoryDef) {
			foreach (var childDef in DefCache.DescendantThingDefsOf(categoryDef)) {
				if (settings.filter.Allows(childDef))
					return true;
			}
		}
		return false;
	}

	internal static bool ShouldPreferForMinimum(
		StorageSettings settings,
		Thing thing,
		IntVec3? storeCell,
		Map? map,
		StorageEvaluationCache? evaluation
	) {
		if (!SupportsExactStorage(settings) || !Manager.TryGetProfile(settings, out var profile) || !profile.Enabled)
			return false;
		var parent = ParentForCell(settings, storeCell, map);
		var quotas = evaluation?.MatchingQuotas(profile, thing) ?? profile.MatchingQuotas(thing);
		if (quotas.Count == 0 || !RefillGate.AllowsRefill(settings))
			return false;
		var underMin = false;
		foreach (var quota in quotas) {
			decimal count = profile.CountFor(quota, parent, evaluation) + EnrouteStockFor(settings, quota, parent, evaluation);
			if (quota.HasMax && count >= quota.MaxStock)
				return false;
			if (quota.HasMin && count < quota.MinStock)
				underMin = true;
		}
		return underMin;
	}

	internal static int DestinationCountLimit(
		StorageSettings settings,
		Thing thing,
		bool preferMinimum,
		IntVec3 storeCell,
		Map map,
		StorageEvaluationCache? evaluation
	) {
		if (!Manager.TryGetProfile(settings, out var profile) || !profile.Enabled)
			return NO_LIMIT;
		var parent = ParentForCell(settings, storeCell, map);
		var quotas = evaluation?.MatchingQuotas(profile, thing) ?? profile.MatchingQuotas(thing);
		var thingDef = InnerDefOf(thing);
		int limit = NO_LIMIT;
		if (preferMinimum) {
			foreach (var quota in quotas) {
				if (!quota.HasMin)
					continue;
				decimal count = profile.CountFor(quota, parent, evaluation) + EnrouteStockFor(settings, quota, parent, evaluation);
				decimal remaining = quota.MinStock - count;
				if (remaining > 0m)
					limit = Math.Min(limit, AmountUtility.StockToRawCeiling(remaining, thingDef));
			}
		}
		foreach (var quota in quotas) {
			if (!quota.HasMax)
				continue;
			decimal count = profile.CountFor(quota, parent, evaluation) + EnrouteStockFor(settings, quota, parent, evaluation);
			limit = Math.Min(limit, AmountUtility.StockToRawFloor(quota.MaxStock - count, thingDef));
		}
		var balance = MinimumBalance.For(settings, profile, parent, evaluation);
		var balanceLimit = balance.CountLimit(thing, storeCell, map);
		if (balanceLimit != NO_LIMIT)
			limit = Math.Min(limit, balanceLimit);
		return Math.Max(0, limit);
	}

	internal static int SourceExcessLimit(Thing thing, StorageEvaluationCache? evaluation) {
		if (
			!thing.Spawned
			|| StoreUtility.CurrentHaulDestinationOf(thing)?.GetStoreSettings() is not { } settings
			|| !Manager.TryGetProfile(settings, out var profile)
			|| !profile.Enabled
		)
			return NO_LIMIT;
		var parent = ParentForStoredThing(settings, thing);
		var quotas = evaluation?.MatchingQuotas(profile, thing) ?? profile.MatchingQuotas(thing);
		var thingDef = InnerDefOf(thing);
		int limit = NO_LIMIT;
		foreach (var quota in quotas) {
			if (!quota.HasMax)
				continue;
			decimal excess = profile.CountFor(quota, parent, evaluation) - quota.MaxStock;
			if (excess > 0m)
				limit = Math.Min(limit, AmountUtility.StockToRawCeiling(excess, thingDef));
		}
		return limit;
	}

	internal static bool TryGetCapacity(StorageSettings settings, ISlotGroupParent? parent, out int capacity) {
		capacity = 0;
		var cells = StorageCells(settings, parent, out var map);
		if (map is null || cells is null)
			return false;
		foreach (var cell in cells)
			capacity += Math.Max(1, cell.GetMaxItemsAllowedInCell(map));
		return true;
	}

	internal static bool TryFindPreferredUnderMinCell(
		Thing thing,
		Pawn carrier,
		Map map,
		StoragePriority currentPriority,
		Faction faction,
		StorageEvaluationCache? evaluation,
		out IntVec3 cell,
		out IHaulDestination destination
	) => TryFindCell(thing, carrier, map, currentPriority, faction, CellSearchMode.PreferMinimum, evaluation, out cell, out destination);

	internal static bool TryFindAllowedCell(
		Thing thing,
		Pawn carrier,
		Map map,
		StoragePriority currentPriority,
		Faction faction,
		StorageEvaluationCache? evaluation,
		out IntVec3 cell,
		out IHaulDestination destination
	) => TryFindCell(thing, carrier, map, currentPriority, faction, CellSearchMode.AnyAllowed, evaluation, out cell, out destination);

	internal static bool CanReceiveAt(IntVec3 cell, Map map, Thing thing, StorageEvaluationCache? evaluation) {
		var slotGroup = cell.GetSlotGroup(map);
		if (slotGroup is null || !slotGroup.parent.Accepts(thing))
			return false;
		if (IsCurrentStorageScope(thing, slotGroup.Settings, slotGroup.parent))
			return false;
		int limit = DestinationCountLimit(slotGroup.Settings, thing, false, cell, map, evaluation);
		return limit is NO_LIMIT or > 0;
	}

	internal static ISlotGroupParent? ParentForStoredThing(StorageSettings? settings, Thing thing) {
		if (settings is null || !UseSeparateLinkedStorage(settings) || !thing.Spawned)
			return null;
		var slotGroup = thing.Position.GetSlotGroup(thing.Map);
		if (slotGroup is null)
			return null;
		return settings.owner is StorageGroup group && slotGroup.StorageGroup == group ? slotGroup.parent : null;
	}

	internal static bool JobTargetsScope(StorageSettings settings, ISlotGroupParent? parent, Map map, Job job)
		=> MatchesScope(settings, parent, map, job.GetTarget(TargetIndex.B));

	internal static Map? MapFor(StorageSettings settings, ISlotGroupParent? parent) {
		if (parent is not null)
			return parent.Map;
		return settings.owner switch {
			StorageGroup group           => group.Map,
			IHaulDestination destination => destination.Map,
			_                            => null
		};
	}

	internal static ThingDef InnerDefOf(Thing thing) => (thing.GetInnerIfMinified() ?? thing).def;

	internal static int StockSlotsFor(Thing thing) => AmountUtility.StockSlots(AmountUtility.RawToStock(thing.stackCount, InnerDefOf(thing)));

	internal static bool IsCurrentStorageScope(Thing thing, StorageSettings settings, ISlotGroupParent parent) {
		if (!thing.Spawned)
			return false;
		var sourceParent = thing.Position.GetSlotGroup(thing.Map)?.parent;
		if (sourceParent is null)
			return false;
		if (sourceParent == parent)
			return true;
		return sourceParent.GetStoreSettings() == settings && !UseSeparateLinkedStorage(settings);
	}

	internal static decimal ModEnrouteStockFor(StorageSettings settings, Quota quota, ISlotGroupParent? parent, Job? ignoredJob) {
		var map = MapFor(settings, parent);
		if (map is null)
			return 0m;
		var count = 0m;
		foreach (var (pawn, job) in EnumerateActiveJobs(map)) {
			if (job == ignoredJob)
				continue;
			count += ModEnrouteStockForJob(settings, quota, parent, map, pawn, job);
		}
		return count;
	}

	internal static IEnumerable<(Pawn Claimant, Job Job)> EnumerateActiveJobs(Map map) {
		var seen = new HashSet<Job>();
		foreach (var pawn in map.mapPawns.AllPawnsSpawned) {
			if (pawn.jobs is null)
				continue;
			foreach (var job in pawn.jobs.AllJobs()) {
				if (job is null || !seen.Add(job))
					continue;
				yield return (pawn, job);
			}
		}
		foreach (var reservation in map.reservationManager.ReservationsReadOnly) {
			var job = reservation.Job;
			if (job is null || !seen.Add(job))
				continue;
			yield return (reservation.Claimant, job);
		}
	}

	private static bool TryFindCell(
		Thing thing,
		Pawn carrier,
		Map map,
		StoragePriority currentPriority,
		Faction faction,
		CellSearchMode mode,
		StorageEvaluationCache? evaluation,
		out IntVec3 cell,
		out IHaulDestination destination
	) {
		cell = IntVec3.Invalid;
		destination = null!;
		var closestDist = float.MaxValue;
		var foundPriority = StoragePriority.Unstored;
		var start = thing.SpawnedOrAnyParentSpawned ? thing.PositionHeld : carrier.PositionHeld;
		var allowSamePriority = mode == CellSearchMode.PreferMinimum;
		foreach (var group in map.haulDestinationManager.AllGroupsListInPriorityOrder) {
			var priority = group.Settings.Priority;
			if ((int)priority < (int)currentPriority || (!allowSamePriority && (int)priority <= (int)currentPriority))
				break;
			if ((int)priority < (int)foundPriority)
				break;
			if (!ShouldConsiderGroup(group, faction))
				continue;
			foreach (var candidate in group.CellsList) {
				var slotGroup = group ?? candidate.GetSlotGroup(map);
				if (slotGroup is null || !slotGroup.parent.Accepts(thing))
					continue;
				if (IsCurrentStorageScope(thing, slotGroup.Settings, slotGroup.parent))
					continue;
				if (!CandidateAllowed(slotGroup.Settings, thing, candidate, map, mode, evaluation))
					continue;
				int dist = (start - candidate).LengthHorizontalSquared;
				if (dist > closestDist || !StoreUtility.IsGoodStoreCell(candidate, map, thing, carrier, faction))
					continue;
				cell = candidate;
				destination = slotGroup.parent;
				closestDist = dist;
				foundPriority = priority;
			}
		}
		return cell.IsValid && destination is not null;
	}

	private static bool CandidateAllowed(
		StorageSettings settings,
		Thing thing,
		IntVec3 cell,
		Map map,
		CellSearchMode mode,
		StorageEvaluationCache? evaluation
	) {
		return mode switch {
			CellSearchMode.PreferMinimum => ShouldPreferForMinimum(settings, thing, cell, map, evaluation),
			// TryFindCell has already checked Accepts and IsCurrentStorageScope for this candidate.
			CellSearchMode.AnyAllowed => DestinationCountLimit(settings, thing, false, cell, map, evaluation) is NO_LIMIT or > 0,
			_                         => false
		};
	}

	private static bool ShouldConsiderGroup(ISlotGroup? group, Faction faction) {
		if (group is not SlotGroup slotGroup || !slotGroup.parent.HaulDestinationEnabled)
			return false;
		return slotGroup.parent is not Thing building || building.Faction == faction;
	}

	private static ISlotGroupParent? ParentForCell(StorageSettings settings, IntVec3? cell, Map? map) {
		if (!UseSeparateLinkedStorage(settings))
			return null;
		if (cell is { } c && map is not null) {
			var slotGroup = c.GetSlotGroup(map);
			return settings.owner is StorageGroup cellGroup && slotGroup?.StorageGroup == cellGroup ? slotGroup.parent : null;
		}
		if (settings.owner is StorageGroup storageGroup) {
			foreach (var member in storageGroup.members) {
				if (member is ISlotGroupParent parent)
					return parent;
			}
		}
		return null;
	}

	private static int UsedStockSlots(StorageSettings settings, ISlotGroupParent? parent, StorageEvaluationCache? evaluation) {
		if (evaluation is not null)
			return evaluation.UsedStockSlots(settings, parent);
		var slots = 0;
		foreach (var thing in HeldThings(settings, parent))
			slots += StockSlotsFor(thing);
		return slots;
	}

	private static int IncomingStockSlots(
		StorageSettings settings,
		Thing thing,
		int raw,
		ISlotGroupParent? parent,
		IntVec3? cell,
		Map? map
	) {
		if (raw <= 0)
			return 0;
		var stackSpace = cell is { } c && map is not null
			? ExistingStackSpaceInCell(thing, c, map)
			: ExistingStackSpaceInScope(settings, thing, parent);
		var extraRaw = Math.Max(0, raw - stackSpace);
		return AmountUtility.StockSlots(AmountUtility.RawToStock(extraRaw, InnerDefOf(thing)));
	}

	private static int ExistingStackSpaceInScope(StorageSettings settings, Thing thing, ISlotGroupParent? parent) {
		var space = 0;
		foreach (var heldThing in HeldThings(settings, parent)) {
			if (heldThing.CanStackWith(thing))
				space += Math.Max(0, heldThing.def.stackLimit - heldThing.stackCount);
		}
		return space;
	}

	private static int ExistingStackSpaceInCell(Thing thing, IntVec3 cell, Map map) {
		var space = 0;
		foreach (var heldThing in cell.GetThingList(map)) {
			if (heldThing.CanStackWith(thing))
				space += Math.Max(0, heldThing.def.stackLimit - heldThing.stackCount);
		}
		return space;
	}

	private static IEnumerable<IntVec3>? StorageCells(StorageSettings settings, ISlotGroupParent? parent, out Map? map) {
		if (parent is not null) {
			map = parent.Map;
			return parent.AllSlotCells();
		}
		switch (settings.owner) {
			case StorageGroup group:
				map = group.Map;
				return group.CellsList;
			case ISlotGroupParent settingsParent and IHaulDestination dest:
				map = dest.Map;
				return settingsParent.AllSlotCells();
			default:
				map = null;
				return null;
		}
	}

	private static decimal EnrouteStockFor(
		StorageSettings settings,
		Quota quota,
		ISlotGroupParent? parent,
		StorageEvaluationCache? evaluation = null
	) {
		if (evaluation is not null)
			return evaluation.EnrouteStockFor(settings, quota, parent);
		var map = MapFor(settings, parent);
		if (map is null)
			return 0m;
		var count = 0m;
		foreach (var (pawn, job) in EnumerateActiveJobs(map))
			count += EnrouteStockForJob(settings, quota, parent, map, pawn, job);
		return count;
	}

	private static decimal EnrouteStockForJob(
		StorageSettings settings,
		Quota quota,
		ISlotGroupParent? parent,
		Map map,
		Pawn pawn,
		Job job
	) {
		if (job.def != JobDefOf.HaulToCell || job.haulMode != HaulMode.ToCellStorage || !JobTargetsScope(settings, parent, map, job))
			return ModEnrouteStockForJob(settings, quota, parent, map, pawn, job);
		var thing = job.GetTarget(TargetIndex.A).Thing;
		if (thing is null || !quota.Matches(thing))
			return ModEnrouteStockForJob(settings, quota, parent, map, pawn, job);
		int raw = Math.Max(0, Math.Min(job.count, thing.stackCount));
		if (pawn.jobs?.curJob == job && pawn.carryTracker.CarriedThing is { } carried && quota.Matches(carried))
			raw += carried.stackCount;
		return AmountUtility.RawToStock(raw, InnerDefOf(thing)) + ModEnrouteStockForJob(settings, quota, parent, map, pawn, job);
	}

	private static decimal ModEnrouteStockForJob(
		StorageSettings settings,
		Quota quota,
		ISlotGroupParent? parent,
		Map map,
		Pawn pawn,
		Job job
	) {
		var count = 0m;
		foreach (var provider in _enrouteStockProviders) {
			try {
				count += provider(settings, quota, parent, map, pawn, job);
			}
			catch (Exception e) {
				Helper.Logger.Warning($"Enroute stock provider failed: {e.GetType().Name} {e.Message}", true);
			}
		}
		return count;
	}

	// Owns the minimum-balance state for one (settings, parent) scope: capacity, used slots, unmet-minimum slots,
	// and the list of quotas currently below their minimum. The cluster of free helpers it replaces all shared
	// these locals; collapsing them into one ref struct removes a lot of parameter-passing noise.
	internal readonly ref struct MinimumBalance {
		private readonly StorageSettings _settings;

		private readonly ISlotGroupParent? _parent;

		// True only when RefillGate is open AND capacity is known. When false, all checks become no-ops.
		private readonly bool _gated;

		private readonly int _capacity;

		private readonly int _usedSlots;

		private readonly int _unmetSlots;

		// Each entry is a quota that is currently below its minimum, paired with the remaining stock to fill it.
		// null when no quota is under-min.
		private readonly List<(Quota Quota, decimal Remaining)>? _underMin;

		private MinimumBalance(
			StorageSettings settings,
			ISlotGroupParent? parent,
			bool gated,
			int capacity,
			int usedSlots,
			int unmetSlots,
			List<(Quota Quota, decimal Remaining)>? underMin
		) {
			_settings = settings;
			_parent = parent;
			_gated = gated;
			_capacity = capacity;
			_usedSlots = usedSlots;
			_unmetSlots = unmetSlots;
			_underMin = underMin;
		}

		public static MinimumBalance For(
			StorageSettings settings,
			Profile profile,
			ISlotGroupParent? parent,
			StorageEvaluationCache? cache
		) {
			if (!RefillGate.AllowsRefill(settings) || !TryGetCapacity(settings, parent, out var capacity))
				return new MinimumBalance(settings, parent, false, 0, 0, 0, null);

			List<(Quota Quota, decimal Remaining)>? underMin = null;
			var unmetSlots = 0;
			foreach (var quota in profile.Quotas) {
				if (!profile.QuotaUsable(quota) || !quota.HasMin || !QuotaAllowed(settings, quota))
					continue;
				decimal count = profile.CountFor(quota, parent, cache) + EnrouteStockFor(settings, quota, parent, cache);
				var remaining = quota.MinStock - count;
				if (remaining <= 0m)
					continue;
				unmetSlots += AmountUtility.StockSlots(remaining);
				underMin ??= [];
				underMin.Add((quota, remaining));
			}
			var usedSlots = UsedStockSlots(settings, parent, cache);
			return new MinimumBalance(settings, parent, true, capacity, usedSlots, unmetSlots, underMin);
		}

		public bool CanAccept(Thing thing, IntVec3? cell, Map? map) => CountLimit(thing, cell, map) is NO_LIMIT or > 0;

		public int CountLimit(Thing thing, IntVec3? cell, Map? map) {
			if (!_gated || _unmetSlots <= 0)
				return NO_LIMIT;
			var maxRaw = Math.Max(0, thing.stackCount);
			if (maxRaw <= 0)
				return 0;
			if (ReliefFor(thing, maxRaw).Stock > 0m)
				return NO_LIMIT;
			var available = Math.Max(0, _capacity - _usedSlots);
			var threshold = Math.Min(0, available - _unmetSlots);
			if (BalanceAfterIncoming(thing, maxRaw, cell, map, available) >= threshold)
				return NO_LIMIT;

			var low = 0;
			var high = maxRaw;
			while (low < high) {
				var mid = (low + high + 1) / 2;
				if (BalanceAfterIncoming(thing, mid, cell, map, available) >= threshold)
					low = mid;
				else
					high = mid - 1;
			}
			return low;
		}

		public bool ShouldDisplace(Thing thing) {
			if (!_gated)
				return false;
			if (ContributesToUnmetMinimum(thing))
				return false;
			var available = Math.Max(0, _capacity - _usedSlots);
			var shortage = _unmetSlots - available;
			if (shortage <= 0)
				return false;
			foreach (var heldThing in HeldThings(_settings, _parent)) {
				if (ContributesToUnmetMinimum(heldThing))
					continue;
				var slots = StockSlotsFor(heldThing);
				if (heldThing == thing)
					return shortage > 0;
				shortage -= slots;
				if (shortage <= 0)
					return false;
			}
			return false;
		}

		private bool ContributesToUnmetMinimum(Thing thing) {
			if (_underMin is null)
				return false;
			foreach (var (quota, _) in _underMin) {
				if (quota.Matches(thing))
					return true;
			}
			return false;
		}

		private (int Slots, decimal Stock) ReliefFor(Thing thing, int raw) {
			if (raw <= 0 || _underMin is null)
				return (0, 0m);
			var stock = AmountUtility.RawToStock(raw, InnerDefOf(thing));
			var slots = 0;
			var stockRelief = 0m;
			foreach ((var quota, decimal remaining) in _underMin) {
				if (!quota.Matches(thing))
					continue;
				slots += AmountUtility.StockSlots(remaining) - AmountUtility.StockSlots(remaining - stock);
				stockRelief += Math.Min(remaining, stock);
			}
			return (slots, stockRelief);
		}

		private int BalanceAfterIncoming(Thing thing, int raw, IntVec3? cell, Map? map, int available) {
			var consumed = IncomingStockSlots(_settings, thing, raw, _parent, cell, map);
			(int relief, _) = ReliefFor(thing, raw);
			return available - consumed - Math.Max(0, _unmetSlots - relief);
		}
	}
}

internal sealed class StorageEvaluationCache(Job? ignoredJob = null) {
	private readonly Dictionary<(Profile Profile, ThingDef ThingDef), List<Quota>> _matchingQuotas = [];

	private readonly Dictionary<(StorageSettings Settings, ISlotGroupParent? Parent), ScopeSnapshot> _scopeSnapshots = [];

	private readonly Dictionary<(StorageSettings Settings, ISlotGroupParent? Parent, Quota Quota), decimal> _heldCounts = [];

	private readonly Dictionary<(StorageSettings Settings, ISlotGroupParent? Parent, Quota Quota), decimal> _enrouteCounts = [];

	private enum CountKind {
		Held,
		Enroute
	}

	public bool Contains(StorageSettings? settings, Thing thing) {
		if (settings is null)
			return false;
		var parent = StorageUtility.ParentForStoredThing(settings, thing);
		return SnapshotFor(settings, parent).HeldThings.Contains(thing);
	}

	public List<Quota> MatchingQuotas(Profile profile, Thing thing) => MatchingQuotas(profile, StorageUtility.InnerDefOf(thing));

	public List<Quota> MatchingQuotas(Profile profile, ThingDef thingDef) {
		var key = (profile, thingDef);
		if (_matchingQuotas.TryGetValue(key, out var quotas))
			return quotas;
		quotas = profile.MatchingQuotas(thingDef);
		_matchingQuotas.Add(key, quotas);
		return quotas;
	}

	public decimal CountFor(StorageSettings settings, Quota quota, ISlotGroupParent? parent) {
		var key = (settings, parent, quota);
		if (_heldCounts.TryGetValue(key, out decimal count))
			return count;
		count = SnapshotFor(settings, parent).Sum(quota, CountKind.Held);
		_heldCounts.Add(key, count);
		return count;
	}

	public decimal EnrouteStockFor(StorageSettings settings, Quota quota, ISlotGroupParent? parent) {
		var key = (settings, parent, quota);
		if (_enrouteCounts.TryGetValue(key, out decimal count))
			return count;
		count = SnapshotFor(settings, parent).Sum(quota, CountKind.Enroute) + StorageUtility.ModEnrouteStockFor(settings, quota, parent, ignoredJob);
		_enrouteCounts.Add(key, count);
		return count;
	}

	public int UsedStockSlots(StorageSettings settings, ISlotGroupParent? parent) => SnapshotFor(settings, parent).UsedStockSlots;

	private static void AddEnroute(
		ScopeSnapshot snapshot,
		StorageSettings settings,
		ISlotGroupParent? parent,
		Map map,
		Pawn pawn,
		Job job,
		Job? ignoredJob
	) {
		if (job == ignoredJob)
			return;
		if (job.def != JobDefOf.HaulToCell || job.haulMode != HaulMode.ToCellStorage)
			return;
		if (!StorageUtility.JobTargetsScope(settings, parent, map, job))
			return;
		var thing = job.GetTarget(TargetIndex.A).Thing;
		if (thing is null)
			return;
		snapshot.AddEnroute(StorageUtility.InnerDefOf(thing), Math.Max(0, Math.Min(job.count, thing.stackCount)));
		if (pawn.jobs?.curJob == job && pawn.carryTracker.CarriedThing is { } carried)
			snapshot.AddEnroute(StorageUtility.InnerDefOf(carried), carried.stackCount);
	}

	private ScopeSnapshot SnapshotFor(StorageSettings settings, ISlotGroupParent? parent) {
		var key = (settings, parent);
		if (_scopeSnapshots.TryGetValue(key, out var snapshot))
			return snapshot;

		snapshot = new ScopeSnapshot();
		foreach (var thing in StorageUtility.HeldThings(settings, parent)) {
			snapshot.HeldThings.Add(thing);
			snapshot.AddHeld(StorageUtility.InnerDefOf(thing), thing.stackCount);
		}

		var map = StorageUtility.MapFor(settings, parent);
		if (map is not null) {
			foreach (var (pawn, job) in StorageUtility.EnumerateActiveJobs(map))
				AddEnroute(snapshot, settings, parent, map, pawn, job, ignoredJob);
		}

		_scopeSnapshots.Add(key, snapshot);
		return snapshot;
	}

	private sealed class ScopeSnapshot {
		private readonly Dictionary<ThingDef, decimal> _heldStockByDef = [];

		private readonly Dictionary<ThingDef, decimal> _enrouteStockByDef = [];

		public HashSet<Thing> HeldThings { get; } = [];

		public int UsedStockSlots { get; private set; }

		public void AddHeld(ThingDef thingDef, int rawCount) {
			AddStock(_heldStockByDef, thingDef, rawCount);
			UsedStockSlots += AmountUtility.StockSlots(AmountUtility.RawToStock(rawCount, thingDef));
		}

		public void AddEnroute(ThingDef thingDef, int rawCount) => AddStock(_enrouteStockByDef, thingDef, rawCount);

		public decimal Sum(Quota quota, CountKind kind) {
			var source = kind == CountKind.Held ? _heldStockByDef : _enrouteStockByDef;
			if (quota.ThingDef is { } thingDef)
				return source.GetValueOrDefault(thingDef);
			if (quota.CategoryDef is not { } categoryDef)
				return 0m;
			var count = 0m;
			foreach (var descendant in DefCache.DescendantThingDefsOf(categoryDef))
				count += source.GetValueOrDefault(descendant);
			return count;
		}

		private static void AddStock(Dictionary<ThingDef, decimal> stockByDef, ThingDef thingDef, int rawCount) {
			if (rawCount <= 0)
				return;
			stockByDef[thingDef] = stockByDef.GetValueOrDefault(thingDef) + AmountUtility.RawToStock(rawCount, thingDef);
		}
	}
}