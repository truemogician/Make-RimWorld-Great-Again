using TrueMogician.RimWorld.Utility.Diagnostics;
using UnityEngine;
using Verse;
using Logger = TrueMogician.RimWorld.Utility.Logger;

namespace TrueMogician.RimWorld.ExactStorage;

public class Mod : Verse.Mod {
	public Mod(ModContentPack content) : base(content) {
		Settings.Default = GetSettings<Settings>();
		Diagnostic.AddSink(new LogSink(new Logger($"{ThisAssembly.Info.Title}.Diagnostics") { Enabled = true }));
		Settings.Default.Apply();
	}

	public override string SettingsCategory() => ThisAssembly.Info.Title;

	public override void DoSettingsWindowContents(Rect inRect) => Settings.Default.DrawContents(inRect);
}