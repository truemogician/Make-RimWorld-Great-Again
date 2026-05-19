using HarmonyLib;
using TrueMogician.RimWorld.AnimalHaulExtended.PUAH.Patches;
using TrueMogician.RimWorld.Utility.Attributes;
using Verse;

namespace TrueMogician.RimWorld.AnimalHaulExtended.PUAH;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		var harmony = new Harmony(ThisAssembly.Project.PackageId);
		harmony.PatchFromType(typeof(WorkGiverHaulToInventoryPatches));
		Helper.Logger.Message("Pick Up And Haul support initialized");
	}
}
