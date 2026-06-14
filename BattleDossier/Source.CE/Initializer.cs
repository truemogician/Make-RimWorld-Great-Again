using TrueMogician.RimWorld.BattleDossier.Static;
using Verse;

namespace TrueMogician.RimWorld.BattleDossier.CE;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		Helper.Logger.Message("Combat Extended support initialized");
	}
}