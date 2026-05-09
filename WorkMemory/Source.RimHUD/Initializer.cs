using HarmonyLib;
using TrueMogician.RimWorld.Utility.Attributes;
using TrueMogician.RimWorld.WorkMemory.Static;
using TrueMogician.RimWorld.WorkMemory.RimHUD.Patches;
using Verse;

namespace TrueMogician.RimWorld.WorkMemory.RimHUD;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		var harmony = new Harmony(ThisAssembly.Project.PackageId);
		harmony.PatchFromType(typeof(ActivityValuePatches));
		Helper.Logger.Message("RimHUD support initialized");
	}
}