using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable.Patches;

public static class RestLocationPatches {
	[Flags]
	private enum Hazards : byte {
		None = 0,
		Gas = 1 << 0,
		Pollution = 1 << 1,
		Fire = 1 << 2,
		Corpse = 1 << 3,
		All = Gas | Pollution | Fire | Corpse
	}

	[HarmonyPatch(typeof(JobGiver_GetRest), "IsValidCell")]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPostfix]
	private static void JobGiver_GetRest_IsValidCell_Postfix(Pawn? pawn, IntVec3 cell, ref bool __result) {
		if (!__result)
			return;
		if (pawn?.Map is not { } map || !cell.InBounds(map))
			return;
		if (cell.GetEdifice(map) is Building_Door)
			goto Forbidden;
		var hazards = GetPawnHazards(pawn);
		if (IsHazardousCell(map, cell, hazards))
			goto Forbidden;
		return;
	Forbidden:
		__result = false;
	}

	private static Hazards GetPawnHazards(Pawn pawn) {
		if (pawn.RaceProps is not { } race)
			return Hazards.None;
		return race.IsFlesh ? Hazards.All : Hazards.Fire;
	}

	private static bool IsHazardousCell(Map map, IntVec3 c, Hazards hazards = Hazards.All) {
		if (hazards == Hazards.None)
			return false;
		if (hazards.HasFlag(Hazards.Gas) && map.gasGrid is { } gas) { // Check for hazardous gases
			var value = gas.GetDirect(c);
			value &= ~(0xFFU << (short)GasType.BlindSmoke); // Ignore Blind Smoke
			if (value != 0)
				return true;
		}
		if (hazards.HasFlag(Hazards.Pollution) && map.pollutionGrid is { } pollution) { // Check for pollution
			if (pollution.IsPolluted(c))
				return true;
		}
		if (c.GetThingList(map) is { Count: > 0 } things) { // Check for hazardous things
			if (hazards.HasFlag(Hazards.Fire) && things.Any(t => t is Fire))
				return true;
			if (hazards.HasFlag(Hazards.Corpse) && things.Any(t => t is Corpse))
				return true;
		}
		return false;
	}
}