using TrueMogician.RimWorld.Rimfined.CE.Patches;
using Verse;

namespace TrueMogician.RimWorld.Rimfined.CE;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		Settings.AddFeaturePatches(Features.NoTarget, typeof(NoTargetPatches));
		Helper.Logger.Message("Combat Extended support initialized");
	}
}