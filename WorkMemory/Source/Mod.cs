using HarmonyLib;
using TrueMogician.RimWorld.Utility.Attributes;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.WorkMemory;

public class Mod : Verse.Mod {
	private readonly Harmony _harmony = new(ThisAssembly.Project.PackageId);

	public Mod(ModContentPack content) : base(content) {
		Settings.Default = GetSettings<Settings>();
		LongEventHandler.QueueLongEvent(() => _harmony.PatchFromType(typeof(Patches.WorkMemory)), $"{ThisAssembly.Info.Title}-Patch", true, null);
	}

	public override string SettingsCategory() => "WorkMemory.Title".TryTranslate(out var title) ? title : ThisAssembly.Info.Title;

	public override void DoSettingsWindowContents(Rect inRect) => Settings.Default.DrawContents(inRect);
}
