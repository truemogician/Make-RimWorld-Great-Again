using System.Reflection;
using HarmonyLib;
using Verse;

namespace TrueMogician.RimWorld.Profiler;

using static Helper;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		var harmony = new Harmony(ThisAssembly.Project.PackageId);
		harmony.PatchAll(Assembly.GetExecutingAssembly());
		Logger.Message("Initialized");
	}
}