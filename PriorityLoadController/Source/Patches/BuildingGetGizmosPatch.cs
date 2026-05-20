using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.PriorityLoadController.Components;
using TrueMogician.RimWorld.PriorityLoadController.Contents.Command;
using TrueMogician.RimWorld.Utility;
using Verse;

namespace TrueMogician.RimWorld.PriorityLoadController.Patches;

[HarmonyPatch(typeof(Building), nameof(Building.GetGizmos))]
internal static class BuildingGetGizmosPatch {
	[HarmonyPostfix]
	internal static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building __instance) {
		foreach (var gizmo in __result)
			yield return gizmo;
		if (!__instance.Spawned || __instance.Faction != Faction.OfPlayer)
			yield break;
		if (__instance.GetComp<CompPriorityLoadController>() is not null)
			yield break;
		if (__instance.GetComp<CompPowerTrader>() is not { } trader)
			yield break;
		if (trader.Props.PowerConsumption <= 0f)
			yield break;
		if (trader.PowerNet is not { } net)
			yield break;
		if (__instance.Map is not { } map)
			yield break;
		if (CachedMapComponent<PriorityLoadControllerMapComponent>.Get(map) is not { } registry || !registry.HasActiveControllerFor(net))
			yield break;
		yield return new SetLoadPriority(trader);
	}
}