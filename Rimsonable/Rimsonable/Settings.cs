using System;
using System.Collections.Generic;
using TrueMogician.RimWorld.Rimsonable.Patches;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable;

public class Settings : ModSettings {
	private readonly HashSet<Type> _patchTypes = [];

	public Settings() {
		ShieldPatchEnabled = true;
	}

	public static Settings Default { get; internal set; } = null!;

	public bool ShieldPatchEnabled {
		get => this[typeof(CompShieldPatches)];
		set => this[typeof(CompShieldPatches)] = value;
	}

	internal ICollection<Type> PatchTypes => _patchTypes;

	internal bool this[Type patchType] {
		get => _patchTypes.Contains(patchType);
		set {
			if (value)
				_patchTypes.Add(patchType);
			else
				_patchTypes.Remove(patchType);
		}
	}

	public override void ExposeData() {
		base.ExposeData();
		bool shieldPatchEnabled = ShieldPatchEnabled;
		Scribe_Values.Look(ref shieldPatchEnabled, "shieldPatchEnabled", true);
		if (Scribe.mode == LoadSaveMode.LoadingVars)
			ShieldPatchEnabled = shieldPatchEnabled;
	}

	public void Apply() {
		// Placeholder for any future application logic
	}
}