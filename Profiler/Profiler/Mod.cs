using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Profiler;

public class Mod : Verse.Mod {
	public Mod(ModContentPack content) : base(content) {
		Settings.Default = GetSettings<Settings>();
		Settings.Default.Apply();
	}

	public override string SettingsCategory() => ThisAssembly.Info.Title;

	public override void DoSettingsWindowContents(Rect inRect) => Settings.Default.DrawContents(inRect);
}