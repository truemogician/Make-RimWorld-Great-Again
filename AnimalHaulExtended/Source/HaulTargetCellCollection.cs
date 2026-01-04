using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using TrueMogician.RimWorld.Utility;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.AnimalHaulExtended;

[Flags]
public enum HaulTarget : byte {
	Transporter = 1 << 0,
	ConstructionSite = 1 << 1
}

public sealed class HaulTargetCellCollection(Map map) : MapComponent(map) {
	private static readonly FindExtraHaulTargets?[] _finders = new FindExtraHaulTargets[8];

	private readonly HashSet<int>?[] _targetIndexCollections = new HashSet<int>[8];

	static HaulTargetCellCollection() {
		// Register default finders
		RegisterFinder(
			HaulTarget.Transporter,
			map => FindAllThingRectsInGroup(map, ThingRequestGroup.Transporter)
		);
		RegisterFinder(
			HaulTarget.ConstructionSite,
			map => FindIncompleteFrames(map)
				.Concat(FindAllThingRectsInGroup(map, ThingRequestGroup.Blueprint))
		);
		// Remove completed construction sites periodically
		OnTick += map => {
			if (!map.IsHashIntervalTick(120))
				return;
			var indices = map.GetHaulTargetCellCollection()?.GetIndexCollection(HaulTarget.ConstructionSite);
			if (indices is null)
				return;
			var indicesToRemove = new List<int>();
			foreach (int index in indices) {
				var cell = map.IndexToCell(index);
				foreach (var thing in map.thingGrid.ThingsAt(cell)) {
					switch (thing) {
						case Blueprint:
						case Frame frame when !frame.IsCompleted(): goto ConstructionSiteFound;
					}
				}
				indicesToRemove.Add(index);
			ConstructionSiteFound:;
			}
			indices.ExceptWith(indicesToRemove);
		};
	}

	public delegate IEnumerable<CellRect> FindExtraHaulTargets(Map map);

	public static event Action<Map> OnTick;

	public static void RegisterFinder(HaulTarget target, FindExtraHaulTargets finder) {
		if (!target.IsSingleBitFlag)
			throw new ArgumentException("Flag must be a single valid ExtraHaulTarget value.", nameof(target));
		_finders[target.LsbIndex] = finder;
	}

	public static IEnumerable<Thing> FindAllThingsInGroup(Map map, ThingRequestGroup group) {
		var things = map.listerThings.ThingsInGroup(group);
		if (things is null || things.Count == 0)
			yield break;
		foreach (var thing in things) {
			if (!thing.Spawned)
				continue;
			yield return thing;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static IEnumerable<CellRect> FindAllThingRectsInGroup(Map map, ThingRequestGroup group)
		=> FindAllThingsInGroup(map, group).Select(t => t.OccupiedRect());

	public static IEnumerable<CellRect> FindIncompleteFrames(Map map)
		=> FindAllThingsInGroup(map, ThingRequestGroup.BuildingFrame)
			.OfType<Frame>()
			.Where(f => !f.IsCompleted())
			.Select(f => f.OccupiedRect());

	public override void FinalizeInit() {
		base.FinalizeInit();
		RebuildFromMapState();
	}

	public override void MapComponentTick() => OnTick.Invoke(map);

	public void Add(CellRect rect, HaulTarget target) {
		rect.ClipInsideMap(map);
		var set = GetIndexCollection(target);
		set.UnionWith(rect.Cells.Select(c => map.CellToIndex(c)));
	}

	public void Remove(CellRect rect, HaulTarget target) {
		rect.ClipInsideMap(map);
		var set = GetIndexCollection(target);
		set.ExceptWith(rect.Cells.Select(c => map.CellToIndex(c)));
	}

	public IEnumerable<IntVec3> EnumerateTargetCells(HaulTarget target)
		=> GetIndexCollection(target).Select(index => map.IndexToCell(index));

	public IntVec3? FindClosestCell(IntVec3 center, HaulTarget target, Func<IntVec3, bool>? filter = null) {
		var enumerable = EnumerateTargetCells(target);
		if (filter is not null)
			enumerable = enumerable.Where(filter);
		return enumerable.ToArray() is { Length: > 0 } cells ? cells.MinBy(c => c.DistanceToSquared(center)) : null;
	}

	public IntVec3? FindClosestReachableCell(Pawn pawn, HaulTarget target)
		=> FindClosestCell(
			pawn.Position,
			target,
			c => pawn.CanReachImmediate(c, PathEndMode.Touch)
		);

	private HashSet<int> GetIndexCollection(HaulTarget target) {
		var set = _targetIndexCollections[target.LsbIndex];
		if (set is null)
			_targetIndexCollections[target.LsbIndex] = set = [];
		return set;
	}

	private void RebuildFromMapState() {
		for (var i = 0; i < _finders.Length; i++) {
			var finder = _finders[i];
			if (finder is null)
				continue;
			var flag = (HaulTarget)(1 << i);
			foreach (var rect in finder(map))
				Add(rect, flag);
		}
	}
}

public static class HaulTargetCellCollectionExtensions {
	private static readonly ConditionalWeakTable<Map, HaulTargetCellCollection?> MapComponentCache = new();

	public static HaulTargetCellCollection? GetHaulTargetCellCollection(this Map map) {
		if (!MapComponentCache.TryGetValue(map, out var component)) {
			component = map.GetComponent<HaulTargetCellCollection>();
			MapComponentCache.Add(map, component);
		}
		return component;
	}
}