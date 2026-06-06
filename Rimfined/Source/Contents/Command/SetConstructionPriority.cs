using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using TrueMogician.RimWorld.Rimfined.Components;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Rimfined.Contents.Command;

using static ConstructionPriorityUtility;

public sealed class SetConstructionPriority : Verse.Command {
	private const int _GROUP_KEY = 0x52F1C1D;

	private readonly Thing _target;

	private List<Thing>? _targets;

	public SetConstructionPriority(Thing target) {
		_target = target;
		icon = TexCommand.Install;
		defaultLabel = Translate("Commands.Label");
		defaultDesc = Translate("Commands.Description");
		groupKeyIgnoreContent = _GROUP_KEY;
	}

	public override string TopRightLabel => Manager[_target].Label().CapitalizeFirst();

	private IEnumerable<Thing> Targets => _targets ?? [_target];

	public override void ProcessInput(Event ev) {
		base.ProcessInput(ev);
		var targets = Targets.ToArray();
		var options = new List<FloatMenuOption>();
		foreach (StoragePriority priority in Enum.GetValues(typeof(StoragePriority))) {
			if (priority == StoragePriority.Unstored)
				continue;
			var localPriority = priority;
			options.Add(
				new FloatMenuOption(
					localPriority.Label().CapitalizeFirst(),
					() => {
						foreach (var thing in targets)
							Manager[thing] = localPriority;
					}
				)
			);
		}
		Find.WindowStack.Add(new FloatMenu(options));
	}

	public override bool InheritInteractionsFrom(Gizmo other) {
		if (other is not SetConstructionPriority command)
			return false;
		_targets ??= [_target];
		_targets.AddRange(command.Targets);
		return false;
	}
}