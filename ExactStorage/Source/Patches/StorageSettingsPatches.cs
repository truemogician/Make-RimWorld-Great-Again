using HarmonyLib;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage.Patches;

internal static class StorageSettingsPatches {
	[HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.ExposeData))]
	[HarmonyPostfix]
	internal static void StorageSettings_ExposeData_Postfix(StorageSettings __instance) {
		switch (Scribe.mode) {
			case LoadSaveMode.Saving: {
				if (!Manager.TryGetProfile(__instance, out var profile) || !profile.HasData)
					return;
				profile.PruneInactive();
				Scribe_Deep.Look(ref profile, "ExactStorageProfile", __instance);
				return;
			}
			case LoadSaveMode.LoadingVars: {
				Profile? profile = null;
				Scribe_Deep.Look(ref profile, "ExactStorageProfile", __instance);
				Manager.SetProfile(__instance, profile);
				break;
			}
		}
	}

	[HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.CopyFrom))]
	[HarmonyPostfix]
	internal static void StorageSettings_CopyFrom_Postfix(StorageSettings __instance, StorageSettings other)
		=> Manager.CopyProfile(__instance, other);

	[HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.AllowedToAccept), typeof(Thing))]
	[HarmonyPostfix]
	internal static void StorageSettings_AllowedToAcceptThing_Postfix(StorageSettings __instance, Thing t, ref bool __result) {
		if (!__result)
			return;
		if (!__instance.SupportsExactStorage || !Manager.TryGetProfile(__instance, out var profile) || !profile.Enabled)
			return;
		__result = __instance.Allows(t, __instance.Contains(t));
	}

	[HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.AllowedToAccept), typeof(ThingDef))]
	[HarmonyPostfix]
	internal static void StorageSettings_AllowedToAcceptThingDef_Postfix(StorageSettings __instance, ThingDef t, ref bool __result) {
		if (!__result || !__instance.SupportsExactStorage || !Manager.TryGetProfile(__instance, out var profile))
			return;
		if (!profile.Enabled || __instance.UseSeparateLinkedStorage)
			return;
		foreach (var quota in profile.MatchingQuotas(t)) {
			if (quota.HasMax && profile.CountFor(quota) >= quota.MaxStock) {
				__result = false;
				return;
			}
		}
	}

	[HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.Accepts))]
	[HarmonyPostfix]
	internal static void BuildingStorage_Accepts_Postfix(Building_Storage __instance, Thing t, ref bool __result) {
		if (!__result)
			return;
		var settings = __instance.GetStoreSettings();
		if (!Manager.TryGetProfile(settings, out var profile) || !profile.Enabled)
			return;
		__result = settings.Allows(t, settings.Contains(t), __instance);
	}
}