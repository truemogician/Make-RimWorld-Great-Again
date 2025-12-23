using System.Reflection;
using HarmonyLib;
using Verse;

namespace TrueMogician.RimWorld.Profiler;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		var harmony = new Harmony(ThisAssembly.Project.PackageId);
		harmony.PatchAll(Assembly.GetExecutingAssembly());
		Log.Message($"[{ThisAssembly.Info.Title}] Initialized");
	}
}