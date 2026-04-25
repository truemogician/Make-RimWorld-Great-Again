using HarmonyLib;
using TrueMogician.RimWorld.ExactStorage.Patches;
using TrueMogician.RimWorld.Utility.Attributes;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		var harmony = new Harmony(ThisAssembly.Project.PackageId);
		harmony.PatchFromType(typeof(StorageBehaviorPatches));
		harmony.PatchFromType(typeof(StorageSettingsPatches));
		harmony.PatchFromType(typeof(StorageUIPatches));
		Helper.Logger.Message("Initialized");
	}
}