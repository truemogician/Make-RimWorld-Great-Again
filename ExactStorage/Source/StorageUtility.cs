using System;
using System.Collections.Generic;
using System.Linq;
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

	public static void AddEnrouteStockProvider(EnrouteStockProvider provider) {
		if (!_enrouteStockProviders.Contains(provider))
			_enrouteStockProviders.Add(provider);
	}

	internal static bool TryFindPreferredUnderMinCell(
		Thing thing,
		Pawn carrier,
		Map map,
		StoragePriority currentPriority,
		Faction faction,
		out IntVec3 cell,
		out IHaulDestination destination
	) => TryFindCell(thing, carrier, map, currentPriority, faction, CellSearchMode.PreferMinimum, out cell, out destination);

	internal static bool TryFindAllowedCell(
		Thing thing,
		Pawn carrier,
		Map map,
		StoragePriority currentPriority,
		Faction faction,
		out IntVec3 cell,
		out IHaulDestination destination
	) => TryFindCell(thing, carrier, map, currentPriority, faction, CellSearchMode.AnyAllowed, out cell, out destination);

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
				if (thing.IsCurrentStorageScope(slotGroup.Settings, slotGroup.parent))
					continue;
				if (!CandidateAllowed(slotGroup.Settings, thing, candidate, map, mode))
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

	private static bool CandidateAllowed(StorageSettings settings, Thing thing, IntVec3 cell, Map map, CellSearchMode mode) {
		var allowed = mode switch {
			CellSearchMode.PreferMinimum => settings.ShouldPreferForMinimum(thing, cell, map),
			// TryFindCell has already checked Accepts and IsCurrentStorageScope for this candidate.
			CellSearchMode.AnyAllowed => settings.DestinationCountLimit(thing, false, cell, map) is NO_LIMIT or > 0,
			_                         => false
		};
		if (!allowed)
			return false;
		return thing.SourceCountLimit(cell, map) is NO_LIMIT or > 0;
	}

	private static int SourceMinimumLimit(Thing thing, IntVec3 storeCell, Map map) {
		if (
			!thing.Spawned
			|| StoreUtility.CurrentHaulDestinationOf(thing)?.GetStoreSettings() is not { } sourceSettings
			|| !Manager.TryGetProfile(sourceSettings, out var sourceProfile)
			|| !sourceProfile.Enabled
			|| CanDrainForHigherPriorityMinimum(sourceSettings, thing, storeCell, map)
		)
			return NO_LIMIT;
		var parent = sourceSettings.ParentForStoredThing(thing);
		var thingDef = thing.InnerDef;
		int limit = NO_LIMIT;
		foreach (var quota in sourceProfile.MatchingQuotas(thing)) {
			if (!quota.HasMin || !sourceSettings.QuotaAllowed(quota))
				continue;
			decimal count = sourceProfile.CountFor(quota, parent) + EnrouteStockFor(sourceSettings, quota, parent);
			limit = Math.Min(limit, AmountUtility.StockToRawFloor(count - quota.MinStock, thingDef));
		}
		return limit;
	}

	private static bool CanDrainForHigherPriorityMinimum(StorageSettings sourceSettings, Thing thing, IntVec3 storeCell, Map map) {
		var slotGroup = storeCell.GetSlotGroup(map);
		if (slotGroup is null)
			return false;
		var destinationSettings = slotGroup.Settings;
		return (int)destinationSettings.Priority > (int)sourceSettings.Priority
			&& destinationSettings.ShouldPreferForMinimum(thing, storeCell, map);
	}

	private static bool ShouldConsiderGroup(ISlotGroup? group, Faction faction) {
		if (group is not SlotGroup slotGroup || !slotGroup.parent.HaulDestinationEnabled)
			return false;
		return slotGroup.parent is not Thing building || building.Faction == faction;
	}

	private static ISlotGroupParent? ParentForCell(StorageSettings settings, IntVec3? cell, Map? map) {
		if (!settings.UseSeparateLinkedStorage)
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

	private static int UsedStockSlots(StorageSettings settings, ISlotGroupParent? parent) {
		var slots = 0;
		foreach (var thing in settings.HeldThings(parent))
			slots += thing.StockSlots;
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
		return AmountUtility.StockSlots(AmountUtility.RawToStock(extraRaw, thing.InnerDef));
	}

	private static int ExistingStackSpaceInScope(StorageSettings settings, Thing thing, ISlotGroupParent? parent) {
		var space = 0;
		foreach (var heldThing in settings.HeldThings(parent)) {
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

	private static decimal EnrouteStockFor(StorageSettings settings, Quota quota, ISlotGroupParent? parent, Job? ignoredJob = null) {
		var map = settings.MapFor(parent);
		if (map is null)
			return 0m;
		var count = 0m;
		foreach (var (pawn, job) in EnumerateActiveJobs(map)) {
			if (job == ignoredJob)
				continue;
			count += EnrouteStockForJob(settings, quota, parent, map, pawn, job);
		}
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
		if (
			job.def != JobDefOf.HaulToCell
			|| job.haulMode != HaulMode.ToCellStorage
			|| !settings.MatchesScope(parent, map, job.GetTarget(TargetIndex.B))
		)
			return ModEnrouteStockForJob(settings, quota, parent, map, pawn, job);
		var thing = job.GetTarget(TargetIndex.A).Thing;
		if (thing is null || !quota.Matches(thing))
			return ModEnrouteStockForJob(settings, quota, parent, map, pawn, job);
		int raw = Math.Max(0, Math.Min(job.count, thing.stackCount));
		if (pawn.jobs?.curJob == job && pawn.carryTracker.CarriedThing is { } carried && quota.Matches(carried))
			raw += carried.stackCount;
		return AmountUtility.RawToStock(raw, thing.InnerDef) + ModEnrouteStockForJob(settings, quota, parent, map, pawn, job);
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

	/**
	 * Owns the minimum-balance state for one (settings, parent) scope: capacity, used slots, unmet-minimum slots,
	 * and the list of quotas currently below their minimum. The cluster of free helpers it replaces all shared
	 * these locals; collapsing them into one ref struct removes a lot of parameter-passing noise.
	 */
	internal readonly ref struct MinimumBalance {
		private readonly StorageSettings _settings;

		private readonly ISlotGroupParent? _parent;

		private readonly bool _gated;

		private readonly int _capacity;

		private readonly int _usedSlots;

		private readonly int _unmetSlots;

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

		public static MinimumBalance For(StorageSettings settings, Profile profile, ISlotGroupParent? parent, Job? ignoredJob = null) {
			if (!RefillGate.AllowsRefill(settings) || !settings.TryGetCapacity(out var capacity, parent))
				return new MinimumBalance(settings, parent, false, 0, 0, 0, null);
			List<(Quota Quota, decimal Remaining)>? underMin = null;
			var unmetSlots = 0;
			foreach (var quota in profile.Quotas) {
				if (!profile.QuotaValid(quota) || !quota.HasMin || !settings.QuotaAllowed(quota))
					continue;
				decimal count = profile.CountFor(quota, parent) + EnrouteStockFor(settings, quota, parent, ignoredJob);
				var remaining = quota.MinStock - count;
				if (remaining <= 0m)
					continue;
				unmetSlots += AmountUtility.StockSlots(remaining);
				underMin ??= [];
				underMin.Add((quota, remaining));
			}
			var usedSlots = UsedStockSlots(settings, parent);
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
			foreach (var heldThing in _settings.HeldThings(_parent)) {
				if (ContributesToUnmetMinimum(heldThing))
					continue;
				var slots = heldThing.StockSlots;
				if (heldThing == thing)
					return true;
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
			var stock = AmountUtility.RawToStock(raw, thing.InnerDef);
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

	extension(StorageSettings settings) {
		public bool SupportsExactStorage => settings.owner is StorageGroup or ISlotGroupParent;

		public Profile? ExactStorageProfile => Manager.TryGetProfile(settings, out var profile) ? profile : null;

		public bool ExactStorageEnabled => settings is {
			SupportsExactStorage: true,
			ExactStorageProfile.Enabled: true
		};

		public bool SeparateLinkedStorageAvailable {
			get {
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
		}

		public bool UseSeparateLinkedStorage => settings is {
			ExactStorageProfile: { Enabled: true, SeparateLinkedStorages: true },
			SeparateLinkedStorageAvailable: true
		};

		public IEnumerable<Thing> HeldThings(ISlotGroupParent? parent = null) {
			if (parent?.GetSlotGroup() is { } scopedGroup) {
				foreach (var thing in scopedGroup.HeldThings)
					yield return thing;
				yield break;
			}
			switch (settings.owner) {
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

		public bool Contains(Thing thing) {
			var parent = settings.ParentForStoredThing(thing);
			foreach (var heldThing in settings.HeldThings(parent)) {
				if (heldThing == thing)
					return true;
			}
			return false;
		}

		public bool Allows(Thing thing, bool currentlyStored, ISlotGroupParent? parent = null) {
			if (!settings.SupportsExactStorage || !Manager.TryGetProfile(settings, out var profile) || !profile.Enabled)
				return true;
			if (parent is null && currentlyStored)
				parent = settings.ParentForStoredThing(thing);
			if (settings.UseSeparateLinkedStorage && parent is null && !currentlyStored)
				return true;
			var quotas = profile.MatchingQuotas(thing).ToList();
			foreach (var quota in quotas) {
				if (settings.QuotaAllowed(quota) && quota.HasMax && profile.CountFor(quota, parent) > quota.MaxStock)
					return false;
			}
			var balance = MinimumBalance.For(settings, profile, parent);
			if (currentlyStored)
				return !balance.ShouldDisplace(thing);
			if (!balance.CanAccept(thing, null, null))
				return false;
			if (quotas.Count == 0)
				return true;
			if (!RefillGate.AllowsRefill(settings))
				return false;
			foreach (var quota in quotas) {
				if (settings.QuotaAllowed(quota) && quota.HasMax && profile.CountFor(quota, parent) >= quota.MaxStock)
					return false;
			}
			return true;
		}

		public bool ShouldPreferForMinimum(Thing thing, IntVec3? storeCell = null, Map? map = null, Job? ignoredJob = null) {
			if (!settings.SupportsExactStorage || !Manager.TryGetProfile(settings, out var profile) || !profile.Enabled)
				return false;
			var parent = ParentForCell(settings, storeCell, map);
			var quotas = profile.MatchingQuotas(thing).ToList();
			if (quotas.Count == 0 || !RefillGate.AllowsRefill(settings))
				return false;
			var underMin = false;
			foreach (var quota in quotas) {
				decimal count = profile.CountFor(quota, parent) + EnrouteStockFor(settings, quota, parent, ignoredJob);
				if (quota.HasMax && count >= quota.MaxStock)
					return false;
				if (quota.HasMin && count < quota.MinStock)
					underMin = true;
			}
			return underMin;
		}

		public int DestinationCountLimit(Thing thing, bool preferMinimum, IntVec3 storeCell, Map map, Job? ignoredJob = null) {
			if (!Manager.TryGetProfile(settings, out var profile) || !profile.Enabled)
				return NO_LIMIT;
			var parent = ParentForCell(settings, storeCell, map);
			var quotas = profile.MatchingQuotas(thing).ToList();
			var thingDef = thing.InnerDef;
			int limit = NO_LIMIT;
			if (preferMinimum) {
				foreach (var quota in quotas) {
					if (!quota.HasMin)
						continue;
					decimal count = profile.CountFor(quota, parent) + EnrouteStockFor(settings, quota, parent, ignoredJob);
					decimal remaining = quota.MinStock - count;
					if (remaining > 0m)
						limit = Math.Min(limit, AmountUtility.StockToRawCeiling(remaining, thingDef));
				}
			}
			foreach (var quota in quotas) {
				if (!quota.HasMax)
					continue;
				decimal count = profile.CountFor(quota, parent) + EnrouteStockFor(settings, quota, parent, ignoredJob);
				limit = Math.Min(limit, AmountUtility.StockToRawFloor(quota.MaxStock - count, thingDef));
			}
			var balance = MinimumBalance.For(settings, profile, parent, ignoredJob);
			var balanceLimit = balance.CountLimit(thing, storeCell, map);
			if (balanceLimit != NO_LIMIT)
				limit = Math.Min(limit, balanceLimit);
			return Math.Max(0, limit);
		}

		public bool TryGetCapacity(out int capacity, ISlotGroupParent? parent = null) {
			parent ??= ParentForCell(settings, null, null);
			capacity = 0;
			var cells = StorageCells(settings, parent, out var map);
			if (map is null || cells is null)
				return false;
			foreach (var cell in cells)
				capacity += Math.Max(1, cell.GetMaxItemsAllowedInCell(map));
			return true;
		}

		public void NotifyChanged() => settings.owner?.Notify_SettingsChanged();

		public bool MatchesScope(ISlotGroupParent? parent, Map map, LocalTargetInfo target) {
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

		public bool QuotaAllowed(Quota quota) {
			switch (quota) {
				case ThingQuota { ThingDef: { } thingDef }: return settings.filter.Allows(thingDef);
				case ThingCategoryQuota { CategoryDef: { } categoryDef }: {
					foreach (var childDef in DefCache.DescendantThingDefsOf(categoryDef)) {
						if (settings.filter.Allows(childDef))
							return true;
					}
					break;
				}
			}
			return false;
		}

		public ISlotGroupParent? ParentForStoredThing(Thing thing) {
			if (!settings.UseSeparateLinkedStorage || !thing.Spawned)
				return null;
			var slotGroup = thing.Position.GetSlotGroup(thing.Map);
			if (slotGroup is null)
				return null;
			return settings.owner is StorageGroup group && slotGroup.StorageGroup == group ? slotGroup.parent : null;
		}

		public Map? MapFor(ISlotGroupParent? parent) {
			if (parent is not null)
				return parent.Map;
			return settings.owner switch {
				StorageGroup group           => group.Map,
				IHaulDestination destination => destination.Map,
				_                            => null
			};
		}
	}

	extension(Thing thing) {
		public ThingDef InnerDef => (thing.GetInnerIfMinified() ?? thing).def;

		public int StockSlots => AmountUtility.StockSlots(AmountUtility.RawToStock(thing.stackCount, thing.InnerDef));

		public int SourceExcessLimit() {
			if (
				!thing.Spawned
				|| StoreUtility.CurrentHaulDestinationOf(thing)?.GetStoreSettings() is not { } settings
				|| !Manager.TryGetProfile(settings, out var profile)
				|| !profile.Enabled
			)
				return NO_LIMIT;
			var parent = settings.ParentForStoredThing(thing);
			var thingDef = thing.InnerDef;
			int limit = NO_LIMIT;
			foreach (var quota in profile.MatchingQuotas(thing)) {
				if (!quota.HasMax)
					continue;
				decimal excess = profile.CountFor(quota, parent) - quota.MaxStock;
				if (excess > 0m)
					limit = Math.Min(limit, AmountUtility.StockToRawCeiling(excess, thingDef));
			}
			return limit;
		}

		public int SourceCountLimit(IntVec3 storeCell, Map map) {
			var limit = thing.SourceExcessLimit();
			var minLimit = SourceMinimumLimit(thing, storeCell, map);
			if (minLimit != NO_LIMIT)
				limit = Math.Min(limit, minLimit);
			return Math.Max(0, limit);
		}

		public bool IsCurrentStorageScope(StorageSettings settings, ISlotGroupParent parent) {
			if (!thing.Spawned)
				return false;
			var sourceParent = thing.Position.GetSlotGroup(thing.Map)?.parent;
			if (sourceParent is null)
				return false;
			if (sourceParent == parent)
				return true;
			return sourceParent.GetStoreSettings() == settings && !settings.UseSeparateLinkedStorage;
		}
	}

	extension(IntVec3 cell) {
		public bool CanReceiveAt(Map map, Thing thing) {
			var slotGroup = cell.GetSlotGroup(map);
			if (slotGroup is null || !slotGroup.parent.Accepts(thing))
				return false;
			if (thing.IsCurrentStorageScope(slotGroup.Settings, slotGroup.parent))
				return false;
			int limit = slotGroup.Settings.DestinationCountLimit(thing, false, cell, map);
			if (limit <= 0)
				return false;
			int sourceLimit = thing.SourceCountLimit(cell, map);
			return sourceLimit is NO_LIMIT or > 0;
		}
	}
}