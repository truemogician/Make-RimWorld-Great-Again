using HarmonyLib;
using Verse;

namespace TrueMogician.RimWorld.VehicleFrameworkFixes;

public sealed class Mod : Verse.Mod {
	public Mod(ModContentPack content) : base(content) {
		var harmony = new Harmony(ThisAssembly.Project.PackageId);
		LongEventHandler.ExecuteWhenFinished(() => harmony.PatchAll());
	}
}