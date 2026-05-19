using HarmonyLib;
using PickUpAndHaul;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.AnimalHaulExtended.PUAH.Patches;

internal static class WorkGiverHaulToInventoryPatches {
	/// <summary>
	///     Skip pawns with no inventory capacity. PUAH's XML adds <see cref="CompHauledToInventory" /> to every pawn,so its
	///     own <see cref="WorkGiver_HaulToInventory.ShouldSkip" /> lets non-pack animals through,
	///     where Capacity == 0 desyncs <see cref="WorkGiver_HaulToInventory.HasJobOnThing" /> from
	///     <see cref="WorkGiver_HaulToInventory.JobOnThing" />.
	/// </summary>
	[HarmonyPatch(typeof(WorkGiver_HaulToInventory), nameof(WorkGiver_HaulToInventory.ShouldSkip))]
	[HarmonyPostfix]
	internal static void ShouldSkip_Postfix(Pawn pawn, ref bool __result) {
		if (!__result && !MassUtility.CanEverCarryAnything(pawn))
			__result = true;
	}
}