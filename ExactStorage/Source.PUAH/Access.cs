using HarmonyLib;
using PickUpAndHaul;

namespace TrueMogician.RimWorld.ExactStorage.PUAH;

internal static class Access {
	public static readonly AccessTools.FieldRef<JobDriver_UnloadYourHauledInventory, int> CountToDrop =
		AccessTools.FieldRefAccess<JobDriver_UnloadYourHauledInventory, int>("_countToDrop");
}