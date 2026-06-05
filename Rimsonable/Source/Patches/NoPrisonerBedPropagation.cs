using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Rimsonable.Static;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable.Patches;

public static class NoPrisonerBedPropagation {
	// #region Designation Independence
	/**
	 * Vanilla Room.Notify_RoomShapeChanged force-flips every contained bed to ForPrisoners when the room is a prison cell.
	 * Neutralize that assignment so each bed keeps its own designation, while leaving the loop itself intact.
	 */
	[HarmonyPatch(typeof(Room), nameof(Room.Notify_RoomShapeChanged))]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> Room_Notify_RoomShapeChanged_Transpiler(IEnumerable<CodeInstruction> instructions) {
		var setForPrisoners = AccessTools.PropertySetter(typeof(Building_Bed), nameof(Building_Bed.ForPrisoners));
		var replaced = false;
		foreach (var instruction in instructions) {
			if (!replaced && instruction.Calls(setForPrisoners)) {
				yield return new CodeInstruction(OpCodes.Pop) { labels = instruction.labels, blocks = instruction.blocks }; // discard the bool value
				yield return new CodeInstruction(OpCodes.Pop); // discard the bed reference
				replaced = true;
				continue;
			}
			yield return instruction;
		}
		if (!replaced)
			Helper.Logger.Error("Failed to neutralize prisoner bed propagation in Room.Notify_RoomShapeChanged.");
	}

	/**
	 * Vanilla
	 * <see cref="Building_Bed.SetBedOwnerTypeByInterface" />
	 * spreads the new owner type across every sibling bed in the room.
	 * Keep the vanilla flow, but make the room loop see only explicitly selected beds.
	 */
	[HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.SetBedOwnerTypeByInterface))]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> SetBedOwnerTypeByInterface_Transpiler(IEnumerable<CodeInstruction> instructions) {
		var getContainedBeds = AccessTools.PropertyGetter(typeof(Room), nameof(Room.ContainedBeds));
		var getSelectedBedsInRoom = AccessTools.Method(typeof(NoPrisonerBedPropagation), nameof(GetSelectedBedsInRoom));
		var replaced = false;
		foreach (var instruction in instructions) {
			if (!replaced && instruction.Calls(getContainedBeds)) {
				yield return new CodeInstruction(OpCodes.Call, getSelectedBedsInRoom) { labels = instruction.labels, blocks = instruction.blocks };
				replaced = true;
				continue;
			}
			yield return instruction;
		}
		if (!replaced)
			Helper.Logger.Error("Failed to narrow prisoner bed designation changes to selected beds.");
	}
	// #endregion

	// #region Mood Penalty
	/**
	 * Pawns sleeping near prisoners gain a small mood debuff.
	 */
	[HarmonyPatch(typeof(Toils_LayDown), "ApplyBedRelatedEffects")]
	[HarmonyPostfix]
	internal static void ApplyBedRelatedEffects_Postfix(Pawn p, Building_Bed? bed, int delta) {
		if (bed is null || !p.IsHashIntervalTick(250, delta) || p.IsPrisonerOfColony || p.Awake())
			return;
		if (bed.GetRoom() is not { PsychologicallyOutdoors: false } room)
			return;
		if (!room.ContainedBeds.Any(b => b.ForPrisoners))
			return;
		if (room.ContainedAndAdjacentThings.OfType<Pawn>()
			.Any(o => o.IsPrisonerOfColony && !LovePartnerRelationUtility.LovePartnerRelationExists(p, o)))
			p.needs?.mood?.thoughts.memories.TryGainMemory(Defs.Rimsonable_SleptNearPrisoners);
	}

	private static IEnumerable<Building_Bed> GetSelectedBedsInRoom(Room room) =>
		Find.Selector.SelectedObjects.OfType<Building_Bed>().Where(b => b.GetRoom() == room);
	// #endregion
}