using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using RimWorld;

namespace TrueMogician.RimWorld.ExactStorage.SSS.Patches;

internal static class IOPatches {
	private const string _TYPE_NAME = "SaveStorageSettings.IOUtil";

	public static bool Patch(Harmony harmony) {
		var type = IOUtilType();
		if (type is null) {
			Helper.Logger.Warning("Could not find Save Storage Settings IOUtil; exact profile import/export support is disabled");
			return false;
		}

		var save = Target(type, "SaveStorageSettings");
		var load = Target(type, "LoadStorageSettings");
		if (save is null || load is null) {
			Helper.Logger.Warning("Could not find Save Storage Settings storage methods; exact profile import/export support is disabled");
			return false;
		}

		harmony.Patch(save, postfix: new HarmonyMethod(typeof(IOPatches), nameof(IOUtil_SaveStorageSettings_Postfix)));
		harmony.Patch(load, postfix: new HarmonyMethod(typeof(IOPatches), nameof(IOUtil_LoadStorageSettings_Postfix)));
		return true;
	}

	internal static void IOUtil_SaveStorageSettings_Postfix(StorageSettings setting, FileInfo fi, bool __result) {
		if (__result)
			new ProfileFile(fi).Append(setting);
	}

	internal static void IOUtil_LoadStorageSettings_Postfix(StorageSettings settings, FileInfo fi, bool __result) {
		if (__result)
			new ProfileFile(fi).Load(settings);
	}

	private static Type? IOUtilType() {
		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
			if (assembly.GetName().Name != "SaveStorageSettings")
				continue;
			if (assembly.GetType(_TYPE_NAME) is { } type)
				return type;
		}
		return AccessTools.TypeByName(_TYPE_NAME);
	}

	private static MethodInfo? Target(Type type, string name)
		=> AccessTools.Method(type, name, [typeof(StorageSettings), typeof(FileInfo)]);
}
