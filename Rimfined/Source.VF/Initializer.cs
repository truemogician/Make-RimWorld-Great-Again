using HarmonyLib;
using TrueMogician.RimWorld.Rimfined.Patches;
using Vehicles;
using Verse;

namespace TrueMogician.RimWorld.Rimfined.VF;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		NoTargetScopePatches.AddTarget(AccessTools.DeclaredMethod(typeof(TargetingHelper), "BestAttackTarget"));
		Helper.Logger.Message("Vehicle Framework support initialized");
	}
}