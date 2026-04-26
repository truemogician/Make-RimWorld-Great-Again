using RimWorld;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.ExactStorage.PUAH;

internal static class Targeting {
	public static bool Matches(StorageSettings settings, ISlotGroupParent? parent, Map map, LocalTargetInfo target) {
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

	public static bool MatchesAny(StorageSettings settings, ISlotGroupParent? parent, Map map, Job job) {
		if (Matches(settings, parent, map, job.GetTarget(TargetIndex.B)))
			return true;
		if (job.targetQueueB is null)
			return false;
		foreach (var target in job.targetQueueB) {
			if (Matches(settings, parent, map, target))
				return true;
		}
		return false;
	}
}