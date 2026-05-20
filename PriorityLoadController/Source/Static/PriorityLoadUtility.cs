using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using TrueMogician.RimWorld.PriorityLoadController.Components;
using TrueMogician.RimWorld.Utility;
using Verse;

namespace TrueMogician.RimWorld.PriorityLoadController.Static;

public static class PriorityLoadUtility {
	public static readonly StoragePriority[] ValidPriorities =
		Enum.GetValues(typeof(StoragePriority)).Cast<StoragePriority>().Where(p => p != StoragePriority.Unstored).ToArray();

	public static CompPowerTrader SelectPowerOnCandidate(List<CompPowerTrader> candidates, PowerNet net) =>
		SelectHighestPriority(candidates, net, false);

	public static CompPowerTrader SelectShutdownCandidate(List<CompPowerTrader> candidates, PowerNet net) =>
		SelectHighestPriority(candidates, net, true);

	private static CompPowerTrader SelectHighestPriority(List<CompPowerTrader> candidates, PowerNet net, bool lowest) {
		if (candidates.Count == 1)
			return candidates[0];
		var map = net.Map;
		var registry = map is null ? null : CachedMapComponent<PriorityLoadControllerMapComponent>.Get(map);
		if (registry is null || !registry.HasActiveControllerFor(net))
			return candidates.RandomElement();
		// Among devices sharing the extreme priority, pick randomly to preserve vanilla cycling behavior.
		var extreme = lowest ? StoragePriority.Critical : StoragePriority.Unstored;
		var matchCount = 0;
		foreach (var t in candidates) {
			var priority = registry.GetEffectivePriority(net, t);
			if (lowest ? priority < extreme : priority > extreme) {
				extreme = priority;
				matchCount = 1;
			}
			else if (priority == extreme)
				matchCount++;
		}
		int pick = Rand.Range(0, matchCount);
		foreach (var t in candidates) {
			if (registry.GetEffectivePriority(net, t) != extreme)
				continue;
			if (pick == 0)
				return t;
			pick--;
		}
		return candidates.RandomElement();
	}
}