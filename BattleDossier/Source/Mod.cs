using System;
using System.Collections.Generic;
using HarmonyLib;
using TrueMogician.RimWorld.BattleDossier.Patches;
using TrueMogician.RimWorld.Utility.Attributes;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.BattleDossier;

public class Mod : Verse.Mod {
	private static readonly List<Type> _patchTypes = [
		typeof(BattleLogPatches),
		typeof(StatsCollectionPatches)
	];

	public Mod(ModContentPack content) : base(content) {
		Settings.Default = GetSettings<Settings>();
		var harmony = new Harmony(ThisAssembly.Project.PackageId);
		LongEventHandler.QueueLongEvent(() => _patchTypes.ForEach(harmony.PatchFromType), $"{ThisAssembly.Info.Title}-ApplyPatches", true, null);
	}

	public override string SettingsCategory() => "BattleDossier.Title".Translate();

	public override void DoSettingsWindowContents(Rect inRect) => Settings.Default.DoWindowContents(inRect);

	public void AddPatches(params Type[] patchTypes) => _patchTypes.AddRange(patchTypes);
}