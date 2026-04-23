using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Rimsonable.Components;
using TrueMogician.RimWorld.Rimsonable.Static;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.Rimsonable.Patches;

public static class WorkMemory {
	private static readonly AssignmentClosureFinder _finder = new(typeof(Toil), nameof(Toil.tickIntervalAction));

	private static readonly FieldInfo _workLeftField = AccessTools.Field(typeof(JobDriver_DoBill), nameof(JobDriver_DoBill.workLeft));

	private static readonly MethodInfo _applyWorkMemoryMultiplierMethod = AccessTools.Method(typeof(WorkMemory), nameof(ApplyWorkMemoryMultiplier));

	private static readonly MethodInfo _transpileMethod = AccessTools.Method(typeof(WorkMemory), nameof(Transpile));

	private static MethodBase? _tickIntervalAction;

	internal static WorkMemoryComponent Component => CachedGameComponent<WorkMemoryComponent>.Component;

	public static bool TryGetDisplay(Pawn? pawn, string key, out string text) {
		text = string.Empty;
		var recipe = pawn?.CurJob?.RecipeDef;
		if (recipe is null || !IsTrackedRecipe(recipe))
			return false;
		var multiplier = Component.GetMultiplier(pawn!, recipe, 0);
		text = key.Translate(multiplier.ToStringPercent());
		return true;
	}

	[HarmonyPatch(typeof(Toils_Recipe), nameof(Toils_Recipe.DoRecipeWork))]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> Inspect(IEnumerable<CodeInstruction> insts)
		=> _finder.Transpile(insts);

	[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
	[HarmonyPostfix]
	internal static void Inspect(Pawn __instance, ref string __result) {
		if (!TryGetDisplay(__instance, "Rimsonable.WorkMemory.InspectLine", out string line))
			return;
		__result = __result.NullOrEmpty() ? line : $"{__result}\n{line}";
	}

	[PatchHook(PatchHookTiming.AfterPatch)]
	internal static void Patch(Harmony harmony) {
		switch (_finder.Closures.Count) {
			case 0:
				Helper.Logger.Error(
					$"Work Memory could not resolve {nameof(Toils_Recipe.DoRecipeWork)} {nameof(Toil.tickIntervalAction)}.",
					true
				); break;
			case 1 when _tickIntervalAction != _finder.Closures[0]:
				Unpatch(harmony);
				_tickIntervalAction = _finder.Closures[0];
				harmony.Patch(_tickIntervalAction, transpiler: new HarmonyMethod(_transpileMethod));
				break;
			case > 1:
				Helper.Logger.Error(
					$"Work Memory found multiple {nameof(Toils_Recipe.DoRecipeWork)} {nameof(Toil.tickIntervalAction)} closures.",
					true
				); break;
		}
	}

	[PatchHook(PatchHookTiming.BeforeUnpatch)]
	internal static void Unpatch(Harmony harmony) {
		if (_tickIntervalAction is null)
			return;
		harmony.Unpatch(_tickIntervalAction, _transpileMethod);
		_tickIntervalAction = null;
	}

	private static bool IsTrackedRecipe(RecipeDef recipe) {
		if (Settings.Default.WorkMemoryNonQualityRecipes)
			return true;
		return recipe.products?.Any(product => product.thingDef.HasComp(typeof(CompQuality))) == true;
	}

	private static float ApplyWorkMemoryMultiplier(float workDone, JobDriver_DoBill? driver, int delta) {
		if (workDone <= 0f || delta <= 0)
			return workDone;
		var pawn = driver?.pawn;
		var recipe = driver?.job?.RecipeDef;
		if (pawn is null || recipe is null || !IsTrackedRecipe(recipe))
			return workDone;
		float adjusted = workDone * Component.GetMultiplier(pawn, recipe, delta);
		Component.RecordWork(pawn, recipe, delta);
		return adjusted;
	}

	private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions) {
		var codeList = instructions.ToList();
		if (TryInjectWorkMemory(codeList))
			return codeList;
		Helper.Logger.Error(
			$"Work Memory could not find the recipe {nameof(JobDriver_DoBill.workLeft)} subtraction site. Falling back to vanilla behavior.",
			true
		);
		return codeList;
	}

	private static bool TryInjectWorkMemory(List<CodeInstruction> insts) {
		for (var i = 2; i < insts.Count; i++) {
			var inst = insts[i];
			if (!inst.StoresField(_workLeftField))
				continue;
			if (insts[i - 1].opcode != OpCodes.Sub || insts[i - 2].opcode != OpCodes.Mul)
				continue;
			int startIdx = i - 3;
			if (startIdx < 0)
				continue;
			int fieldLoadIdx = insts.FindLastIndex(startIdx, startIdx + 1, instruction => instruction.LoadsField(_workLeftField));
			if (fieldLoadIdx < 1)
				continue;
			if (FindReusableObjectLoad(insts, fieldLoadIdx) is not { } objectLoad)
				continue;
			insts.InsertRange(
				i - 1,
				[
					new CodeInstruction(objectLoad.opcode, objectLoad.operand),
					new CodeInstruction(OpCodes.Ldarg_1),
					new CodeInstruction(OpCodes.Call, _applyWorkMemoryMultiplierMethod)
				]
			);
			return true;
		}
		return false;
	}

	private static CodeInstruction? FindReusableObjectLoad(IReadOnlyList<CodeInstruction> insts, int fieldLoadIdx) {
		for (int i = fieldLoadIdx - 1; i >= 0; i--) {
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
