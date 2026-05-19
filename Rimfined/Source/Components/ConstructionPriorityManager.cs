using System.Collections.Generic;
using System.Linq;
using RimWorld;
using TrueMogician.RimWorld.Rimfined.Contents.WorkGiver;
using TrueMogician.RimWorld.Utility;
using Verse;

namespace TrueMogician.RimWorld.Rimfined.Components;

public sealed class ConstructionPriorityManager : GameComponent {
	private const int _CLEANUP_INTERVAL_TICKS = 2500;

	private Dictionary<int, StoragePriority> _priorities = new();

	public ConstructionPriorityManager(Game game) { }

	public StoragePriority this[Thing thing] {
		get => _priorities.GetValueOrDefault(thing.thingIDNumber, StoragePriority.Normal);
		set => Set(thing, value);
	}

	public void Set(Thing thing, StoragePriority priority) {
		if (priority is StoragePriority.Unstored or StoragePriority.Normal)
			_priorities.Remove(thing.thingIDNumber);
		else
			_priorities[thing.thingIDNumber] = priority;
	}

	public void Transfer(Thing source, Thing? dest) {
		if (!_priorities.Remove(source.thingIDNumber, out var priority) || dest is null)
			return;
		Set(dest, priority);
	}

	public override void GameComponentTick() {
		if (_priorities.Count == 0 || Find.TickManager.TicksGame % _CLEANUP_INTERVAL_TICKS != 0)
			return;
		ClearStale();
	}

	public override void ExposeData() {
		Scribe_Collections.Look(ref _priorities, "constructionPriorities", LookMode.Value, LookMode.Value);
		if (Scribe.mode == LoadSaveMode.PostLoadInit) {
			_priorities ??= new Dictionary<int, StoragePriority>();
			var idsToRemove = _priorities.Where(kvp => kvp.Value is StoragePriority.Unstored or StoragePriority.Normal)
				.Select(kvp => kvp.Key);
			foreach (var id in idsToRemove.ToArray())
				_priorities.Remove(id);
		}
	}

	private void ClearStale() {
		var activeIds = new HashSet<int>();
		foreach (var map in Current.Game.Maps) {
			foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
				activeIds.Add(thing.thingIDNumber);
			foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
				activeIds.Add(thing.thingIDNumber);
		}
		foreach (int id in _priorities.Keys.Where(id => !activeIds.Contains(id)).ToArray())
			_priorities.Remove(id);
	}
}

internal static class ConstructionPriorityUtility {
	private const string _TRANSLATION_KEY_PREFIX = "Rimfined.ConstructionPriority";

	internal static ConstructionPriorityManager Manager => CachedGameComponent<ConstructionPriorityManager>.Component;

	internal static bool UseUnifiedConstructionDelivery =>
		Settings.Default[Features.ConstructionPriority] && Settings.Default.UseUnifiedConstructionDelivery;

	internal static bool ValidTarget(Thing thing) => thing is Blueprint or Frame && thing.Faction == Faction.OfPlayer;

	internal static StoragePriority GetPriority(Thing thing) =>
		CachedGameComponent<ConstructionPriorityManager>.TryGet()?[thing] ?? StoragePriority.Normal;

	internal static bool PrioritizesConstruction(WorkGiver_Scanner scanner) =>
		scanner is WorkGiver_ConstructFinishFrames
			or ConstructDeliverResourcesToConstruction
			or WorkGiver_ConstructDeliverResourcesToFrames
			or WorkGiver_ConstructDeliverResourcesToBlueprints;

	internal static string Translate(string suffix) => $"{_TRANSLATION_KEY_PREFIX}.{suffix}".Translate().Resolve();
}