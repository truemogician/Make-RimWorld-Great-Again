using TrueMogician.RimWorld.Rimsonable.Static;
using TrueMogician.RimWorld.Rimsonable.RimHUD.Patches;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable.RimHUD;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		Settings.AddFeaturePatches(Features.WorkMemory, typeof(ActivityValuePatches));
		Helper.Logger.Message("RimHUD support initialized");
	}
}