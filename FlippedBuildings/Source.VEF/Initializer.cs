using HarmonyLib;
using Verse;

namespace TrueMogician.RimWorld.FlippedBuildings.VEF;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		new Harmony("TrueMogician.FlippedBuildings.VEF").PatchAll(typeof(Initializer).Assembly);
		Helper.Logger.Message("Vanilla Expanded Framework support initialized");
	}
}