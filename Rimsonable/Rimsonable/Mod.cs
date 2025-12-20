using System;
using System.Linq;
using TrueMogician.RimWorld.Utility;
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

		var allFeatures = Enum.GetValues(typeof(Features)).Cast<Features>().ToArray();
		var updatedFeatures = Features.None;
		foreach (var feature in allFeatures) {
			if (!feature.IsSingleBitFlag)
				continue;
			var label = feature.Label ?? feature.Name;
			var enabled = Settings.Default[feature];
			listing.CheckboxLabeled(label, ref enabled);
			if (enabled)
				updatedFeatures |= feature;
		}

		Settings.Default.Features = updatedFeatures;
		Settings.Default.Apply();

		listing.End();
	}
}