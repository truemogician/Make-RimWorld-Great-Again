using MjRimMods.StorageRefillHysteresis;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage.SRH;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		Helper.Logger.Message("Storage Refill Hysteresis support initialized");
	}
}