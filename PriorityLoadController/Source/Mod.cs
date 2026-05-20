using HarmonyLib;
using Verse;

namespace TrueMogician.RimWorld.PriorityLoadController;

public class Mod : Verse.Mod {
	public Mod(ModContentPack content) : base(content) {
		new Harmony(ThisAssembly.Project.PackageId).PatchAll();
	}
}