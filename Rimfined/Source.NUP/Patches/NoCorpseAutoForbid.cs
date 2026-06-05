using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NonUnoPinata;
using Verse;

namespace TrueMogician.RimWorld.Rimfined.NUP.Patches;

internal static class NoCorpseAutoForbidPatches {
	private static readonly MethodInfo[] _targetMethods = [
		AccessTools.Method(
			typeof(Pawn_EquipmentTracker),
			nameof(Pawn_EquipmentTracker.DropAllEquipment),
			[typeof(IntVec3), typeof(bool), typeof(bool)]
		),
		AccessTools.Method(
			typeof(Pawn_InventoryTracker),
			nameof(Pawn_InventoryTracker.DropAllNearPawn),
			[typeof(IntVec3), typeof(bool), typeof(bool)]
		),
		AccessTools.Method(
			typeof(NUPUtility),
			nameof(NUPUtility.DropUnmarkableNearPawn),
			[typeof(Pawn_InventoryTracker), typeof(IntVec3), typeof(bool), typeof(bool)]
		)
	];

	[HarmonyPatch(typeof(NUPUtility), nameof(NUPUtility.DropThings))]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> NUPUtility_DropThings_Transpiler(IEnumerable<CodeInstruction> instructions) {
		var codes = instructions.ToList();
		var replacements = 0;
		for (var i = 0; i < codes.Count; ++i) {
			if (!_targetMethods.Any(m => codes[i].Calls(m)))
				continue;
			var forbidArgIdx = i - 2;
			if (forbidArgIdx < 0 || !Rimfined.Patches.NoCorpseAutoForbidPatches.ReplaceTrueWithFalse(codes[forbidArgIdx]))
				continue;
			++replacements;
		}
		if (replacements != 3)
			Helper.Logger.Error($"Expected to neutralize 3 Non Uno Pinata auto-forbid calls, neutralized {replacements}.");
		return codes;
	}
}