using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using TrueMogician.RimWorld.Utility;
using Verse;

namespace TrueMogician.RimWorld.PriorityLoadController.Components;

public sealed class CompPriorityLoadController : ThingComp {
	private Dictionary<int, StoragePriority> _priorities = new();

	private CompPowerTrader? _powerTrader;

	private CompFlickable? _flickable;

	public PowerNet? PowerNet => PowerTrader?.PowerNet;

	public bool IsActive {
		get {
			if (!parent.Spawned || parent.Faction != Faction.OfPlayer || parent.IsBrokenDown())
				return false;
			if (Flickable is { SwitchIsOn: false })
				return false;
			if (PowerTrader is not { PowerOn: true })
				return false;
			return PowerNet is not null;
		}
	}

	public int ConfiguredCount => _priorities.Count;

	private CompPowerTrader PowerTrader => _powerTrader ??= parent.GetComp<CompPowerTrader>();

	private CompFlickable? Flickable => _flickable ??= parent.GetComp<CompFlickable>();

	public StoragePriority GetPriority(CompPowerTrader trader) =>
		_priorities.GetValueOrDefault(trader.parent.thingIDNumber, StoragePriority.Normal);

	public void SetPriority(CompPowerTrader trader, StoragePriority priority) {
		int id = trader.parent.thingIDNumber;
		if (priority == StoragePriority.Normal)
			_priorities.Remove(id);
		else
			_priorities[id] = priority;
	}

	public override void PostSpawnSetup(bool respawningAfterLoad) {
		base.PostSpawnSetup(respawningAfterLoad);
		if (CachedMapComponent<PriorityLoadControllerMapComponent>.Get(parent.Map) is { } map)
			map.Register(this);
	}

	public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish) {
		base.PostDeSpawn(map, mode);
		if (CachedMapComponent<PriorityLoadControllerMapComponent>.Get(map) is { } component)
			component.Deregister(this);
	}

	public override void PostExposeData() {
		base.PostExposeData();
		Scribe_Collections.Look(ref _priorities, "priorities", LookMode.Value, LookMode.Value);
		if (Scribe.mode == LoadSaveMode.PostLoadInit) {
			_priorities ??= new Dictionary<int, StoragePriority>();
			var stale = _priorities.Where(kvp => kvp.Value is StoragePriority.Unstored or StoragePriority.Normal)
				.Select(kvp => kvp.Key)
				.ToArray();
			foreach (int id in stale)
				_priorities.Remove(id);
		}
	}

	public override string CompInspectStringExtra() {
		var sb = new StringBuilder();
		string activeKey = IsActive
			? "PriorityLoadController.Controller.Active"
			: PowerNet is null
				? "PriorityLoadController.Controller.NotConnected"
				: "PriorityLoadController.Controller.Inactive";
		sb.Append(activeKey.Translate());
		if (_priorities.Count > 0) {
			sb.AppendLine();
			sb.Append("PriorityLoadController.Controller.CustomPriorityCount".Translate(_priorities.Count));
		}
		return sb.ToString();
	}
}

public sealed class CompProperties_PriorityLoadController : CompProperties {
	public CompProperties_PriorityLoadController() => compClass = typeof(CompPriorityLoadController);
}