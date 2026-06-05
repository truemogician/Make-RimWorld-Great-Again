using TrueMogician.RimWorld.Rimfined.NUP.Patches;
using Verse;

namespace TrueMogician.RimWorld.Rimfined.NUP;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		Settings.AddFeaturePatches(Features.NoCorpseAutoForbid, typeof(NoCorpseAutoForbidPatches));
		Helper.Logger.Message("Non Uno Pinata support initialized");
	}
}