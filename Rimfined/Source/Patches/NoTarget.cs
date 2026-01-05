using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Utility;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.Rimfined.Patches;

internal static class NoTargetPatches {
	internal static NoTargetPawnIds NoTargetPawns => CachedGameComponent<NoTargetPawnIds>.Component;

	internal static Texture2D NoTargetIcon => field ??= ContentFinder<Texture2D>.Get("UI/Commands/NoTarget");

	[HarmonyPatch(typeof(AttackTargetFinder), nameof(AttackTargetFinder.IsAutoTargetable))]
	[HarmonyPrefix]
	internal static bool AttackTargetFinder_IsAutoTargetable_Prefix(IAttackTarget target, ref bool __result) {
		if (target?.Thing is Pawn pawn && NoTargetPawns[pawn]) {
			__result = false;
			return false;
		}
		return true;
	}

	// Add toggle gizmo on hostile pawns
	[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
	[HarmonyPostfix]
	internal static IEnumerable<Gizmo> Pawn_GetGizmos_Postfix(IEnumerable<Gizmo> __result, Pawn __instance) {
		foreach (var g in __result)
			yield return g;
		if (__instance is not { Spawned: true, Dead: false } pawn)
			yield break;
		if (pawn.Faction is not { } f || !f.HostileTo(Faction.OfPlayer))
			yield break;
		yield return new Command_Toggle {
			defaultLabel = "No Target",
			defaultDesc = "Prevent colonists and turrets from auto-targeting this pawn.",
			icon = NoTargetIcon,
			isActive = () => NoTargetPawns[pawn],
			toggleAction = () => NoTargetPawns.Toggle(pawn)
		};
	}

	// --- Auto-mark: hostile pawns with relationships to any spawned free colonist
	[HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
	[HarmonyPostfix]
	internal static void Pawn_SpawnSetup_Postfix(Pawn __instance, bool respawningAfterLoad) {
		if (respawningAfterLoad)
			return;
		if (__instance is not { Dead: false })
			return;
		if (__instance.Faction is not { } f || !f.HostileTo(Faction.OfPlayer))
			return;
		if (NoTargetPawns[__instance])
			return;
		if (HasRelationshipWithAnyColonist(__instance))
			NoTargetPawns[__instance] = true;
	}

	private static bool HasRelationshipWithAnyColonist(Pawn pawn) {
		if (pawn.relations is not { } rel)
			return false;

		// Cheap & safe: only check currently spawned free colonists (usually small list)
		foreach (var colonist in PawnsFinder.AllMaps_FreeColonistsSpawned) {
			if (colonist is null)
				continue;
			// Check both directions; direct relations aren't guaranteed to be symmetric in-memory
			if (rel.DirectRelations.Any(r => r.otherPawn == colonist))
				return true;
			if (colonist.relations is { } cRel && cRel.DirectRelations.Any(r => r.otherPawn == pawn))
				return true;
		}

		return false;
	}
}

public sealed class NoTargetPawnIds : GameComponent {
	private HashSet<string> _noTargetPawnIds = new(StringComparer.Ordinal);

	public NoTargetPawnIds(Game game) { }

	public bool this[Pawn pawn] {
		get => _noTargetPawnIds.Contains(pawn.GetUniqueLoadID());
		set {
			string? id = pawn.GetUniqueLoadID();
			if (value)
				_noTargetPawnIds.Add(id);
			else
				_noTargetPawnIds.Remove(id);
		}
	}

	public void Toggle(Pawn pawn) {
		string? id = pawn.GetUniqueLoadID();
		if (!_noTargetPawnIds.Add(id))
			_noTargetPawnIds.Remove(id);
	}

	public override void ExposeData() {
		Scribe_Collections.Look(ref _noTargetPawnIds, "noTargetPawnIds", LookMode.Value);
		if (Scribe.mode == LoadSaveMode.PostLoadInit)
			_noTargetPawnIds ??= new HashSet<string>(StringComparer.Ordinal);
	}
}