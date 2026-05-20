using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Rimfined.Components;
using TrueMogician.RimWorld.Rimfined.Contents.Command;
using Verse;

namespace TrueMogician.RimWorld.Rimfined.Patches;

using static ConstructionPriorityUtility;

internal static class ConstructionPriorityPatches {
	[HarmonyPatch(typeof(Blueprint), nameof(Blueprint.GetGizmos))]
	[HarmonyPostfix]
	internal static IEnumerable<Gizmo> Blueprint_GetGizmos_Postfix(IEnumerable<Gizmo> __result, Blueprint __instance) {
		foreach (var gizmo in __result)
			yield return gizmo;
		if (ValidTarget(__instance))
			yield return new SetConstructionPriority(__instance);
	}

	[HarmonyPatch(typeof(Frame), nameof(Frame.GetGizmos))]
	[HarmonyPostfix]
	internal static IEnumerable<Gizmo> Frame_GetGizmos_Postfix(IEnumerable<Gizmo> __result, Frame __instance) {
		foreach (var gizmo in __result)
			yield return gizmo;
		if (ValidTarget(__instance))
			yield return new SetConstructionPriority(__instance);
	}

	[HarmonyPatch(typeof(Blueprint_Build), "MakeSolidThing")]
	[HarmonyPostfix]
	internal static void Blueprint_Build_MakeSolidThing_Postfix(Blueprint_Build __instance, Thing __result) {
		if (__result is Frame frame)
			Manager.Transfer(__instance, frame);
	}

	[HarmonyPatch(typeof(Frame), nameof(Frame.FailConstruction))]
	[HarmonyPrefix]
	internal static void Frame_FailConstruction_Prefix(Frame __instance, out FailedConstructionState __state) {
		__state = new FailedConstructionState(__instance);
		Manager[__instance] = StoragePriority.Normal;
	}

	[HarmonyPatch(typeof(Frame), nameof(Frame.FailConstruction))]
	[HarmonyPostfix]
	internal static void Frame_FailConstruction_Postfix(FailedConstructionState __state) {
		if (__state.Priority == StoragePriority.Normal || __state.Map is not { } map)
			return;
		var blueprints = map.blueprintGrid[__state.Position];
		if (blueprints.NullOrEmpty())
			return;
		foreach (var blueprint in blueprints.OfType<Blueprint_Build>()) {
			if (blueprint.EntityToBuild() == __state.EntityToBuild && blueprint.EntityToBuildStuff() == __state.Stuff) {
				Manager[blueprint] = __state.Priority;
				return;
			}
		}
	}

	[HarmonyPatch(typeof(WorkGiver_Scanner), "get_Prioritized")]
	[HarmonyPostfix]
	internal static void WorkGiver_Scanner_Prioritized_Postfix(WorkGiver_Scanner __instance, ref bool __result) {
		if (PrioritizesConstruction(__instance))
			__result = true;
	}

	[HarmonyPatch(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.GetPriority), typeof(Pawn), typeof(TargetInfo))]
	[HarmonyPostfix]
	internal static void WorkGiver_Scanner_GetPriority_Postfix(WorkGiver_Scanner __instance, TargetInfo t, ref float __result) {
		if (!PrioritizesConstruction(__instance) || t.Thing is not { } thing)
			return;
		__result = (float)GetPriority(thing);
	}

	[HarmonyPatch(typeof(WorkGiver), nameof(WorkGiver.ShouldSkip))]
	[HarmonyPostfix]
	internal static void WorkGiver_ShouldSkip_Postfix(WorkGiver __instance, ref bool __result) {
		if (!UseUnifiedConstructionDelivery)
			return;
		if (__instance is WorkGiver_ConstructDeliverResourcesToFrames or WorkGiver_ConstructDeliverResourcesToBlueprints)
			__result = true;
	}

	[HarmonyPatch(typeof(WorkGiver_ConstructDeliverResources), "ResourceDeliverJobFor")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> WorkGiver_ConstructDeliverResources_ResourceDeliverJobFor_Transpiler(
		IEnumerable<CodeInstruction> instructions
	) {
		var codes = instructions.ToList();
		var makingForField = AccessTools.Field(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.makingFor));
		var replacements = 0;
		for (var i = 0; i < codes.Count; ++i) {
			if (codes[i].opcode != OpCodes.Ldsfld || !Equals(codes[i].operand, makingForField))
				continue;
			for (var j = i + 1; j < codes.Count - 1 && j <= i + 6; ++j) {
				if (!IsBeq(codes[j].opcode))
					continue;
				if (!IsLeave(codes[j + 1].opcode))
					break;
				codes[j + 1].opcode = OpCodes.Br;
				codes[j + 1].operand = codes[j].operand;
				++replacements;
				i = j + 1;
				break;
			}
		}
		if (replacements != 2)
			Helper.Logger.Warning($"Expected to patch 2 construction material scan early exits, patched {replacements}.", true);
		return codes;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsBeq(OpCode opCode) => opCode == OpCodes.Beq || opCode == OpCodes.Beq_S;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsLeave(OpCode opCode) => opCode == OpCodes.Leave || opCode == OpCodes.Leave_S;

	internal readonly struct FailedConstructionState(Frame frame) {
		public readonly Map? Map = frame.Map;

		public readonly IntVec3 Position = frame.Position;

		public readonly BuildableDef EntityToBuild = frame.def.entityDefToBuild;

		public readonly ThingDef? Stuff = frame.Stuff;

		public readonly StoragePriority Priority = GetPriority(frame);
	}
}