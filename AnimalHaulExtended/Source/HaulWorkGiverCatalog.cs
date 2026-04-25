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
		if (def.giverClass == typeof(WorkGiver_DoBill))
			return false;
		return def.giverClass != typeof(WorkGiver_Strip);
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