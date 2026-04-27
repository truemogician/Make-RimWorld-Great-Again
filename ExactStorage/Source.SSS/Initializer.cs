using HarmonyLib;
using TrueMogician.RimWorld.ExactStorage.SSS.Patches;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage.SSS;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		var harmony = new Harmony(ThisAssembly.Project.PackageId);
		if (IOPatches.Patch(harmony))
			Helper.Logger.Message("Save Storage Settings support initialized");
	}
}