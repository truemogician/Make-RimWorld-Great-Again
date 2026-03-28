using CombatExtended;
using TrueMogician.RimWorld.Rimsonable.CE.Patches;
using TrueMogician.RimWorld.Rimsonable.Static;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable.CE;

using BasePatches = Rimsonable.Patches;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		BasePatches.AllowGrenadesThroughShields.AddLaunchVerb(typeof(Verb_LaunchProjectileCE));
		Settings.AddFeaturePatches(Features.EnhanceArtilleryMarkers, typeof(EnhanceArtilleryMarkers));
		Helper.Logger.Message("Combat Extended support initialized");
	}
}