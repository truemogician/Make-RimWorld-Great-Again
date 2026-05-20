using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.PriorityLoadController.Static;
using Verse;

namespace TrueMogician.RimWorld.PriorityLoadController.Patches;

[HarmonyPatch(typeof(PowerNet), nameof(PowerNet.PowerNetTick))]
internal static class PowerNetTickPatch {
	private static readonly FieldInfo _partsWantingPowerOnField = AccessTools.Field(typeof(PowerNet), "partsWantingPowerOn");

	private static readonly FieldInfo _potentialShutdownPartsField = AccessTools.Field(typeof(PowerNet), "potentialShutdownParts");

	private static readonly MethodInfo _selectPowerOnMethod = AccessTools.Method(
		typeof(PriorityLoadUtility),
		nameof(PriorityLoadUtility.SelectPowerOnCandidate)
	);

	private static readonly MethodInfo _selectShutdownMethod = AccessTools.Method(
		typeof(PriorityLoadUtility),
		nameof(PriorityLoadUtility.SelectShutdownCandidate)
	);

	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions) {
		var list = instructions.ToList();
		var replaced = ReplaceRandomElementCalls(list);
		if (!replaced.PowerOn) {
			Helper.Logger.Error(
				$"Could not locate {nameof(PowerNet)}.partsWantingPowerOn RandomElement site; load prioritization will not apply to power-on selection.",
				true
			);
		}
		if (!replaced.Shutdown) {
			Helper.Logger.Error(
				$"Could not locate {nameof(PowerNet)}.potentialShutdownParts RandomElement site; load prioritization will not apply to shutdown selection.",
				true
			);
		}
		return list;
	}

	private static (bool PowerOn, bool Shutdown) ReplaceRandomElementCalls(List<CodeInstruction> insts) {
		var powerOn = false;
		var shutdown = false;
		for (var i = 0; i < insts.Count; i++) {
			var inst = insts[i];
			if (inst.opcode != OpCodes.Call && inst.opcode != OpCodes.Callvirt)
				continue;
			if (inst.operand is not MethodInfo { Name: nameof(GenCollection.RandomElement) })
				continue;
			var source = FindPrecedingListField(insts, i);
			MethodInfo? helper = null;
			if (source == _partsWantingPowerOnField) {
				helper = _selectPowerOnMethod;
				powerOn = true;
			}
			else if (source == _potentialShutdownPartsField) {
				helper = _selectShutdownMethod;
				shutdown = true;
			}
			if (helper is null)
				continue;
			var injection = new CodeInstruction(OpCodes.Ldarg_0);
			injection.MoveLabelsFrom(insts[i]);
			insts.Insert(i, injection);
			insts[i + 1] = new CodeInstruction(OpCodes.Call, helper);
			i++;
		}
		return (powerOn, shutdown);
	}

	private static FieldInfo? FindPrecedingListField(IReadOnlyList<CodeInstruction> insts, int callIdx) {
		for (int i = callIdx - 1; i >= 0; i--) {
			var inst = insts[i];
			if (inst.opcode == OpCodes.Ldsfld && inst.operand is FieldInfo field)
				return field;
		}
		return null;
	}
}