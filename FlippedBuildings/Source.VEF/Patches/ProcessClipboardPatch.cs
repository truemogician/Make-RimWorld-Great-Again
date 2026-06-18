using System.Collections.Generic;
using HarmonyLib;
using PipeSystem;
using TrueMogician.RimWorld.FlippedBuildings.Core;
using Verse;

namespace TrueMogician.RimWorld.FlippedBuildings.VEF.Patches;

// PipeSystem's process clipboard is keyed by ThingDef, so copy/paste of processes never crosses a twin and its
// canonical. After the processor tab renders, mirror each clipboard entry onto the opposite def so paste is offered on both.
[HarmonyPatch(typeof(ITab_Processor), "FillTab")]
internal static class ProcessClipboardPatch {
	[HarmonyPostfix]
	internal static void Postfix() {
		var clipboard = ProcessUtility.Clipboard;
		if (clipboard is not { Count: > 0 })
			return;
		foreach (var key in new List<ThingDef>(clipboard.Keys)) {
			if (FlipRegistry.GetTwin(key) is { } twin && !clipboard.ContainsKey(twin))
				clipboard[twin] = clipboard[key];
		}
	}
}
