using HarmonyLib;
using TrueMogician.RimWorld.ExactStorage.PUAH.Patches;
using TrueMogician.RimWorld.Utility.Attributes;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage.PUAH;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		var harmony = new Harmony(ThisAssembly.Project.PackageId);
		harmony.PatchFromType(typeof(CapacityPatches));
		harmony.PatchFromType(typeof(UnloadPatches));
		Enroute.Register();
		Helper.Logger.Message("Pick Up And Haul support initialized");
	}
}