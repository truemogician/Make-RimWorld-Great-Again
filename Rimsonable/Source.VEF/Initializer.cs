using TrueMogician.RimWorld.Rimsonable.Static;
using TrueMogician.RimWorld.Rimsonable.VEF.Patches;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable.VEF;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		Settings.AddFeaturePatches(Features.AllowGrenadesThroughShields, typeof(CompShieldBubblePatches));
		Helper.Logger.Message("Vanilla Expanded Framework support initialized");
	}
}