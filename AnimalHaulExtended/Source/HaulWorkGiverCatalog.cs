using System.Collections.Generic;
using System.Linq;
using RimWorld;

namespace TrueMogician.RimWorld.AnimalHaulExtended;

internal static class HaulWorkGiverCatalog {
	public static IReadOnlyList<WorkGiverDef> AllHaulingWorkGivers => field ??= BuildAllHaulingWorkGivers();

	public static IReadOnlyList<WorkGiverDef> HaulCapabilityWorkGivers => field ??= BuildHaulCapabilityWorkGivers();

	public static bool IsHaulCapabilityWorkGiver(WorkGiverDef def) {
		if (def.workType != WorkTypeDefOf.Hauling)
			return false;
		if (typeof(WorkGiver_DoBill).IsAssignableFrom(def.giverClass))
			return false;
		return !typeof(WorkGiver_Strip).IsAssignableFrom(def.giverClass);
	}

	private static IReadOnlyList<WorkGiverDef> BuildAllHaulingWorkGivers() {
		if (WorkTypeDefOf.Hauling is not { workGiversByPriority: { } workGivers })
			return [];
		return workGivers
			.Where(def => def.Worker != null)
			.ToArray();
	}

	private static IReadOnlyList<WorkGiverDef> BuildHaulCapabilityWorkGivers()
		=> AllHaulingWorkGivers
			.Where(IsHaulCapabilityWorkGiver)
			.ToArray();
}