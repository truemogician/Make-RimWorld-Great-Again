using HarmonyLib;
using TrueMogician.RimWorld.ExactStorage.SSS.Patches;
using TrueMogician.RimWorld.Utility.Attributes;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage.SSS;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		var harmony = new Harmony(ThisAssembly.Project.PackageId);
		harmony.PatchFromType(typeof(IOPatches));
		Helper.Logger.Message("Save Storage Settings support initialized");
	}
}
