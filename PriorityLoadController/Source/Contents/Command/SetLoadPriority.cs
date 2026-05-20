using System.Collections.Generic;
using RimWorld;
using TrueMogician.RimWorld.PriorityLoadController.Components;
using TrueMogician.RimWorld.PriorityLoadController.Static;
using TrueMogician.RimWorld.Utility;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.PriorityLoadController.Contents.Command;

public sealed class SetLoadPriority : Verse.Command {
	private const int _GROUP_KEY = 0x5070_4C50;

	private readonly CompPowerTrader _target;

	private List<CompPowerTrader>? _targets;

	public SetLoadPriority(CompPowerTrader target) {
		_target = target;
		icon = TexCommand.Install;
		defaultLabel = "PriorityLoadController.Command.SetPriority.Label".Translate();
		defaultDesc = "PriorityLoadController.Command.SetPriority.Description".Translate();
		groupKeyIgnoreContent = _GROUP_KEY;
	}

	public override string TopRightLabel => GetDisplayedPriority(_target).Label().CapitalizeFirst();

	private IEnumerable<CompPowerTrader> Targets => _targets ?? [_target];

	public override void ProcessInput(Event ev) {
		base.ProcessInput(ev);
		var targets = new List<CompPowerTrader>(Targets);
		var options = new List<FloatMenuOption>(PriorityLoadUtility.ValidPriorities.Length);
		foreach (var priority in PriorityLoadUtility.ValidPriorities) {
			var localPriority = priority;
			options.Add(
				new FloatMenuOption(
					localPriority.Label().CapitalizeFirst(),
					() => {
						foreach (var trader in targets)
							ApplyPriority(trader, localPriority);
					}
				)
			);
		}
		Find.WindowStack.Add(new FloatMenu(options));
	}

	public override bool InheritInteractionsFrom(Gizmo other) {
		if (other is not SetLoadPriority command)
			return false;
		_targets ??= [_target];
		_targets.AddRange(command.Targets);
		return false;
	}

	private static StoragePriority GetDisplayedPriority(CompPowerTrader trader) {
		if (trader.parent.Map is not { } map || trader.PowerNet is not { } net)
			return StoragePriority.Normal;
		var registry = CachedMapComponent<PriorityLoadControllerMapComponent>.Get(map);
		return registry?.GetEffectivePriority(net, trader) ?? StoragePriority.Normal;
	}

	private static void ApplyPriority(CompPowerTrader trader, StoragePriority priority) {
		if (trader.parent.Map is not { } map || trader.PowerNet is not { } net)
			return;
		var registry = CachedMapComponent<PriorityLoadControllerMapComponent>.Get(map);
		if (registry is null)
			return;
		foreach (var controller in registry.ActiveControllersFor(net))
			controller.SetPriority(trader, priority);
	}
}