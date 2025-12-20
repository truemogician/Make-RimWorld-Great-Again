using CombatExtended;
using TrueMogician.RimWorld.Rimsonable.Static;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable.CE;

using BasePatches = Rimsonable.Patches;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		BasePatches.CompShieldPatches.AddLaunchVerb(typeof(Verb_LaunchProjectileCE));
		Helper.Logger.Message("Combat Extended support initialized");
	}
}