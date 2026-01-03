using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Rimfined;

public class Mod : Verse.Mod {
	public Mod(ModContentPack content) : base(content) {
		Settings.Default = GetSettings<Settings>();
		LongEventHandler.QueueLongEvent(() => Settings.Default.Apply(), $"{ThisAssembly.Info.Title}-ApplySettings", true, null);
	}

	public override string SettingsCategory() => ThisAssembly.Info.Title;

	public override void DoSettingsWindowContents(Rect inRect) => Settings.Default.DrawContents(inRect);
}