using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.Rimfined.Patches;

public static class NoCorpseAutoForbidPatches {
	private static readonly MethodInfo _dropAllEquipment = AccessTools.Method(
		typeof(Pawn_EquipmentTracker),
		nameof(Pawn_EquipmentTracker.DropAllEquipment),
		[typeof(IntVec3), typeof(bool), typeof(bool)]
	);

	private static readonly MethodInfo _dropAllNearPawn = AccessTools.Method(
		typeof(Pawn_InventoryTracker),
		nameof(Pawn_InventoryTracker.DropAllNearPawn),
		[typeof(IntVec3), typeof(bool), typeof(bool)]
	);

	public static bool ReplaceTrueWithFalse(CodeInstruction inst) {
		if (!LoadsTrue(inst))
			return false;
		inst.opcode = OpCodes.Ldc_I4_0;
		inst.operand = null;
		return true;
	}

	[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> Pawn_Kill_Transpiler(IEnumerable<CodeInstruction> instructions) {
		var setForbiddenIfOutsideHomeArea = AccessTools.Method(
			typeof(ForbidUtility),
			nameof(ForbidUtility.SetForbiddenIfOutsideHomeArea),
			[typeof(Thing)]
		);
		var replaced = false;
		foreach (var instruction in instructions) {
			if (!replaced && instruction.Calls(setForbiddenIfOutsideHomeArea)) {
				yield return new CodeInstruction(OpCodes.Pop) { labels = instruction.labels, blocks = instruction.blocks };
				replaced = true;
				continue;
			}
			yield return instruction;
		}
		if (!replaced)
			Helper.Logger.Error("Failed to neutralize corpse auto-forbid in Pawn.Kill.");
	}

	[HarmonyPatch(typeof(Pawn), nameof(Pawn.DropAndForbidEverything))]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> Pawn_DropAndForbidEverything_Transpiler(IEnumerable<CodeInstruction> instructions) {
		var codes = instructions.ToList();
		var replacements = 0;
		for (var i = 0; i < codes.Count; ++i) {
			if (!codes[i].Calls(_dropAllEquipment) && !codes[i].Calls(_dropAllNearPawn))
				continue;
			var forbidArgIdx = i - 2;
			if (forbidArgIdx < 0 || !ReplaceTrueWithFalse(codes[forbidArgIdx]))
				continue;
			++replacements;
		}
		if (replacements != 2)
			Helper.Logger.Error($"Expected to neutralize 2 death drop auto-forbid calls, neutralized {replacements}.");
		return codes;
	}

	private static bool LoadsTrue(CodeInstruction inst)
		=> inst.opcode == OpCodes.Ldc_I4_1
			|| inst.opcode == OpCodes.Ldc_I4 && inst.operand is 1
			|| inst.opcode == OpCodes.Ldc_I4_S && inst.operand is sbyte and 1
			|| inst.opcode == OpCodes.Ldc_I4_S && inst.operand is byte and 1;
}