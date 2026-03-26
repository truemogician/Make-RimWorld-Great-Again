using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Rimsonable.Components;
using TrueMogician.RimWorld.Rimsonable.Static;
using TrueMogician.RimWorld.Utility;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.Rimsonable.Patches;

[HarmonyPatch]
public static class WorkMemory {
	private static readonly FieldInfo _JOB_DRIVER_DO_BILL_WORK_LEFT = AccessTools.Field(typeof(JobDriver_DoBill), nameof(JobDriver_DoBill.workLeft));

	private static readonly MethodInfo _APPLY_WORK_MEMORY_TO_WORK_DONE = AccessTools.Method(typeof(WorkMemory), nameof(ApplyWorkMemoryMultiplier));

	internal static WorkMemoryComponent Component => CachedGameComponent<WorkMemoryComponent>.Component;

	[HarmonyTargetMethod]
	private static MethodBase TargetMethod() {
		var toil = Toils_Recipe.DoRecipeWork();
		return toil.tickIntervalAction?.Method
			?? throw new InvalidOperationException("Could not resolve Toils_Recipe.DoRecipeWork tickIntervalAction.");
	}

	[HarmonyTranspiler]
	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
		var codeList = instructions.ToList();
		if (TryInjectWorkMemory(codeList))
			return codeList;
		Helper.Logger.Error("Work Memory could not find the recipe workLeft subtraction site. Falling back to vanilla behavior.", true);
		return codeList;
	}

	private static bool IsTrackedRecipe(RecipeDef recipe) => recipe.products?.Any(product => product.thingDef.HasComp(typeof(CompQuality))) == true;

	private static float ApplyWorkMemoryMultiplier(float workDone, JobDriver_DoBill driver, int delta) {
		if (workDone <= 0f || delta <= 0)
			return workDone;
		var pawn = driver?.pawn;
		var recipe = driver?.job?.RecipeDef;
		if (pawn == null || recipe == null || !IsTrackedRecipe(recipe))
			return workDone;
		var adjusted = workDone * Component.GetMultiplier(pawn, recipe, delta);
		Component.RecordWork(pawn, recipe, delta);
		return adjusted;
	}

	private static bool TryInjectWorkMemory(List<CodeInstruction> insts) {
		for (var i = 2; i < insts.Count; i++) {
			var inst = insts[i];
			if (!inst.StoresField(_JOB_DRIVER_DO_BILL_WORK_LEFT))
				continue;
			if (insts[i - 1].opcode != OpCodes.Sub || insts[i - 2].opcode != OpCodes.Mul)
				continue;
			int startIdx = i - 3;
			if (startIdx < 0)
				continue;
			int fieldLoadIdx = insts.FindLastIndex(startIdx, startIdx + 1, instruction => instruction.LoadsField(_JOB_DRIVER_DO_BILL_WORK_LEFT));
			if (fieldLoadIdx < 1)
				continue;
			if (FindReusableObjectLoad(insts, fieldLoadIdx) is not { } objectLoad)
				continue;
			insts.InsertRange(
				i - 1,
				[
					new CodeInstruction(objectLoad.opcode, objectLoad.operand),
					new CodeInstruction(OpCodes.Ldarg_1),
					new CodeInstruction(OpCodes.Call, _APPLY_WORK_MEMORY_TO_WORK_DONE)
				]
			);
			return true;
		}
		return false;
	}

	private static CodeInstruction? FindReusableObjectLoad(IReadOnlyList<CodeInstruction> insts, int fieldLoadIdx) {
		for (var i = fieldLoadIdx - 1; i >= 0; i--) {
			var inst = insts[i];
			if (inst.opcode == OpCodes.Dup || inst.opcode == OpCodes.Nop)
				continue;
			if (inst.IsLdloc() || inst.IsLdarg() || inst.opcode == OpCodes.Ldsfld)
				return inst;
			break;
		}
		return null;
	}
}