using System;
using System.Collections.Generic;
using System.Linq;
using PickUpAndHaul;
using RimWorld;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.ExactStorage.PUAH;

internal static class Enroute {
	public static void Register() => StorageUtility.AddEnrouteStockProvider(CountForJob);

	private static decimal CountForJob(StorageSettings settings, Quota quota, ISlotGroupParent? parent, Map map, Pawn pawn, Job job) {
		if (job.def == PickUpAndHaulJobDefOf.HaulToInventory)
			return CountHaulToInventory(settings, quota, parent, map, pawn, job);
		return job.def == PickUpAndHaulJobDefOf.UnloadYourHauledInventory ? CountUnload(settings, quota, parent, map, pawn, job) : 0m;
	}

	private static decimal CountHaulToInventory(StorageSettings settings, Quota quota, ISlotGroupParent? parent, Map map, Pawn pawn, Job job) {
		if (!TargetsScope(settings, parent, map, job))
			return 0m;
		var count = 0m;
		var current = job.GetTarget(TargetIndex.A).Thing;
		if (current is { Spawned: true } && quota.Matches(current))
			count += StockFor(current, job.count);
		if (job.targetQueueA is not null && job.countQueue is not null) {
			foreach (var pair in job.targetQueueA.Zip(job.countQueue, (target, raw) => (target, raw))) {
				var thing = pair.target.Thing;
				if (thing is not null && quota.Matches(thing))
					count += StockFor(thing, pair.raw);
			}
		}
		return count + CountTrackedInventory(quota, pawn);
	}

	private static decimal CountUnload(StorageSettings settings, Quota quota, ISlotGroupParent? parent, Map map, Pawn pawn, Job job) {
		if (!StorageUtility.MatchesScope(settings, parent, map, job.GetTarget(TargetIndex.B)))
			return 0m;
		var thing = job.GetTarget(TargetIndex.A).Thing;
		return thing is not null && quota.Matches(thing) ? StockFor(thing, CountToDrop(pawn, job, thing)) : 0m;
	}

	private static bool TargetsScope(StorageSettings settings, ISlotGroupParent? parent, Map map, Job job) {
		if (StorageUtility.MatchesScope(settings, parent, map, job.GetTarget(TargetIndex.B)))
			return true;
		if (job.targetQueueB is null)
			return false;
		foreach (var target in job.targetQueueB) {
			if (StorageUtility.MatchesScope(settings, parent, map, target))
				return true;
		}
		return false;
	}

	private static decimal CountTrackedInventory(Quota quota, Pawn pawn) {
		var comp = pawn.TryGetComp<CompHauledToInventory>();
		if (comp is null)
			return 0m;
		var defs = new HashSet<ThingDef>();
		foreach (var thing in comp.GetHashSet()) {
			if (thing is not null)
				defs.Add(thing.def);
		}
		var count = 0m;
		foreach (var thing in pawn.inventory.innerContainer) {
			if (defs.Contains(thing.def) && quota.Matches(thing))
				count += StockFor(thing, thing.stackCount);
		}
		return count;
	}

	private static int CountToDrop(Pawn pawn, Job job, Thing thing) {
		if (pawn.jobs?.curJob == job && pawn.jobs.curDriver is JobDriver_UnloadYourHauledInventory driver) {
			int count = Access.CountToDrop(driver);
			if (count > 0)
				return Math.Min(count, thing.stackCount);
		}
		return job.count > 0 ? Math.Min(job.count, thing.stackCount) : thing.stackCount;
	}

	private static decimal StockFor(Thing thing, int count) {
		int raw = Math.Max(0, Math.Min(count, thing.stackCount));
		return AmountUtility.RawToStock(raw, (thing.GetInnerIfMinified() ?? thing).def);
	}
}