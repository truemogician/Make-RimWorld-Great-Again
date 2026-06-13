using System.Collections.Generic;
using RimWorld;
using TrueMogician.RimWorld.FlippedBuildings.Defs;
using Verse;

namespace TrueMogician.RimWorld.FlippedBuildings.Core;

public static class FlipEligibility {
	private static HashSet<ThingDef>? _preceptBuildings;

	private static HashSet<ThingDef> PreceptBuildings => _preceptBuildings ??= CollectPreceptBuildings();

	public static bool IsEligible(ThingDef def) {
		if (def.category != ThingCategory.Building || !def.BuildableByPlayer || def.generated)
			return false;
		var spec = def.GetModExtension<FlipSpec>();
		if (spec?.allow == false)
			return false;
		if (spec?.allow != true && !IsGeometricallyAsymmetric(def))
			return false;
		return !PreceptBuildings.Contains(def);
	}

	public static bool IsGeometricallyAsymmetric(ThingDef def) {
		var size = def.size;
		if (def.hasInteractionCell && MirrorTransform.IsAsymmetric(def.interactionCellOffset, size))
			return true;
		if (!def.multipleInteractionCellOffsets.NullOrEmpty() && MultipleCellsAsymmetric(def.multipleInteractionCellOffsets, size))
			return true;
		return MirrorerRegistry.AnyAsymmetric(def) || SpecialAsymmetry.IsAsymmetric(def);
	}

	private static bool MultipleCellsAsymmetric(List<IntVec3> offsets, IntVec2 size) {
		var original = new HashSet<IntVec3>(offsets);
		return offsets.Any(o => !original.Contains(MirrorTransform.MirrorCellOffset(o, size)));
	}

	private static HashSet<ThingDef> CollectPreceptBuildings() {
		var result = new HashSet<ThingDef>();
		foreach (var precept in DefDatabase<PreceptDef>.AllDefs) {
			if (precept.buildingDefChances == null)
				continue;
			foreach (var chance in precept.buildingDefChances) {
				if (chance.def != null)
					result.Add(chance.def);
			}
		}
		return result;
	}
}