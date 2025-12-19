using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable;

public class Mod : Verse.Mod {
	public Mod(ModContentPack content) : base(content) {
		Settings.Default = GetSettings<Settings>();
		Initializer.Initialize();
	}

	public override string SettingsCategory() => ThisAssembly.Info.Title;

	public override void DoSettingsWindowContents(Rect inRect) {
		var listing = new Listing_Standard();
		listing.Begin(inRect);

		var shieldPatchEnabled = Settings.Default.ShieldPatchEnabled;
		listing.CheckboxLabeled("Enable Shield Patch", ref shieldPatchEnabled);
		Settings.Default.ShieldPatchEnabled = shieldPatchEnabled;

		Settings.Default.Apply();

		listing.End();
	}
}