using HarmonyLib;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable;

public static class Initializer {
	static Initializer() {
		// Placeholder for any static initialization logic if needed in the future
	}

	public static void ApplyPatches() {
		var harmony = new Harmony(ThisAssembly.Project.PackageId);
		foreach (var patchType in Settings.Default.GetPatchTypes())
			harmony.PatchAll(patchType.Assembly);
		Log.Message($"[{ThisAssembly.Info.Title}] Patches Applied");
	}
}