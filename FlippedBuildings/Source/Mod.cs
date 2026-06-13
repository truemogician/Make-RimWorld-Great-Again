using HarmonyLib;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.FlippedBuildings;

public class Mod : Verse.Mod {
	public Mod(ModContentPack content) : base(content) {
		Settings = GetSettings<Settings>();
		// In the ctor: the generation prefix must be installed before defs load.
		new Harmony(ThisAssembly.Project.PackageId).PatchAll();
	}

	public static Settings Settings { get; private set; } = null!;

	public override string SettingsCategory() => "FlippedBuildings.Title".Translate();

	public override void DoSettingsWindowContents(Rect inRect) => Settings.DoWindowContents(inRect);
}