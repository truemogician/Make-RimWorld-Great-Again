using System.IO;
using HarmonyLib;
using RimWorld;

namespace TrueMogician.RimWorld.ExactStorage.SSS.Patches;

[HarmonyPatch($"{nameof(SaveStorageSettings)}.IOUtil")]
internal static class IOPatches {
	[HarmonyPatch("SaveStorageSettings")]
	[HarmonyPostfix]
	internal static void IOUtil_SaveStorageSettings_Postfix(StorageSettings setting, FileInfo fi, bool __result) {
		if (__result)
			new ProfileFile(fi).Append(setting);
	}

	[HarmonyPatch("LoadStorageSettings")]
	[HarmonyPostfix]
	internal static void IOUtil_LoadStorageSettings_Postfix(StorageSettings settings, FileInfo fi, bool __result) {
		if (__result)
			new ProfileFile(fi).Load(settings);
	}
}
