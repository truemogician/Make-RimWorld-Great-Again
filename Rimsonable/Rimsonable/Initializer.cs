using HarmonyLib;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable;

public static class Initializer {
	static Initializer() {
		// Placeholder for any static initialization logic if needed in the future
	}

	public static void Initialize() {
		var harmony = new Harmony(ThisAssembly.Project.PackageId);
		var patchTypes = Settings.Default.PatchTypes;
		foreach (var patchType in patchTypes)
			harmony.PatchAll(patchType.Assembly);
		Log.Message($"[{ThisAssembly.Info.Title}] Initialized");
	}
}