using System;
using System.Linq;
using EnumsNET;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable;

public class Mod : Verse.Mod {
	public Mod(ModContentPack content) : base(content) {
		Settings.Default = GetSettings<Settings>();
		Initializer.ApplyPatches();
	}

	public override string SettingsCategory() => ThisAssembly.Info.Title;

	public override void DoSettingsWindowContents(Rect inRect) {
		var listing = new Listing_Standard();
		listing.Begin(inRect);

		var features = Enum.GetValues(typeof(Features)).Cast<Features>().ToArray();
		foreach (var feature in features) {
			var label = feature.AsString(EnumFormat.Description);
			if (string.IsNullOrEmpty(label)) // Composite flags have no description
				continue;
			var enabled = Settings.Default[feature];
			var oldValue = enabled;
			listing.CheckboxLabeled(label, ref enabled);
			if (oldValue != enabled)
				Settings.Default[feature] = enabled;
		}

		Settings.Default.Apply();

		listing.End();
	}
}