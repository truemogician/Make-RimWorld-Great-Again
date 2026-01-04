using HarmonyLib;
using Verse;
using CombatExtended;

namespace TrueMogician.RimWorld.Rimfined.CE.Patches;

using static Rimfined.Patches.NoTargetHelper;

internal class NoTargetPatches {
	[HarmonyPatch(typeof(Building_TurretGunCE), "IsValidTarget")]
	[HarmonyPriority(Priority.VeryHigh)]
	[HarmonyPrefix]
	internal static bool Building_TurretGunCE_IsValidTarget_Prefix(Thing? t, ref bool __result) {
		if (t is Pawn pawn && Component[pawn]) {
			__result = false;
			return false;
		}
		return true;
	}
}