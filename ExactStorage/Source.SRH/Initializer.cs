using MjRimMods.StorageRefillHysteresis;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage.SRH;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		RefillGate.Add(settings => {
				var hysteresis = settings.GetHysteresis();
				return hysteresis is null || !hysteresis.Enabled || hysteresis.AllowsRefill();
			}
		);
		Helper.Logger.Message("Storage Refill Hysteresis support initialized");
	}
}