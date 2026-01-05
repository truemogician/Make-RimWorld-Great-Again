using Verse;

namespace TrueMogician.RimWorld.Rimfined.CE;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		Helper.Logger.Message("Combat Extended support initialized");
	}
}