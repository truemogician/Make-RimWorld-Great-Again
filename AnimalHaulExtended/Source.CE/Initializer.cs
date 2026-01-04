using CombatExtended;
using Verse;

namespace TrueMogician.RimWorld.AnimalHaulExtended.CE;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		Helper.Logger.Message("Combat Extended support initialized");
	}
}