using CombatExtended;
using HarmonyLib;
using TrueMogician.RimWorld.Rimfined.Patches;
using TrueMogician.RimWorld.Utility;
using Verse;

namespace TrueMogician.RimWorld.Rimfined.CE.Patches;

internal class NoTargetPatches {
	[HarmonyPatch(typeof(Building_TurretGunCE), "IsValidTarget")]
	[HarmonyPriority(Priority.VeryHigh)]
	[HarmonyPrefix]
	internal static bool Building_TurretGunCE_IsValidTarget_Prefix(Thing? t, ref bool __result) {
		if (t is Pawn pawn && CachedGameComponent<NoTargetPawnIds>.Component[pawn]) {
			__result = false;
			return false;
		}
		return true;
	}
}