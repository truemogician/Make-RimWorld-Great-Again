using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Rimfined.Components;
using TrueMogician.RimWorld.Utility;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.Rimfined.Patches;

internal static class NoTargetPatches {
	internal static NoTargetPawnIds NoTargetPawns => CachedGameComponent<NoTargetPawnIds>.Component;

	internal static Texture2D NoTargetIcon => field ??= ContentFinder<Texture2D>.Get("UI/Commands/NoTarget");

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool SkipTargetFor(Pawn target, IAttackTargetSearcher? searcher) {
		if (searcher?.Thing?.Faction is not { } faction)
			return false;
		return NoTargetPawns[target] && faction == Faction.OfPlayer;
	}

	[HarmonyPatch(typeof(Pawn), nameof(Pawn.ThreatDisabled))]
	[HarmonyPriority(Priority.High)]
	[HarmonyPrefix]
	internal static bool Pawn_ThreatDisabled_Prefix(IAttackTargetSearcher? searcher, Pawn __instance, ref bool __result) {
		if (NoTargetScopePatches.InScope && SkipTargetFor(__instance, searcher)) {
			__result = true;
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

	// Automatically mark hostile pawns with relationships to any spawned free colonist
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

public static class NoTargetScopePatches {
	private static readonly List<MethodBase?> _entries = [
		AccessTools.DeclaredMethod(typeof(AttackTargetFinder), nameof(AttackTargetFinder.BestAttackTarget)),
		AccessTools.DeclaredMethod(typeof(JobDriver_Wait), "CheckForAutoAttack")
	];

	[ThreadStatic]
	private static uint _scopeCounter;

	public static bool InScope => _scopeCounter > 0;

	private static MethodBase?[] ConditionalEntries => field ??= GetConditionalEntries().ToArray();

	public static void AddTarget(MethodBase target) => _entries.Add(target);

	[HarmonyTargetMethods]
	internal static IEnumerable<MethodBase> GetTargetMethods() {
		foreach (var target in _entries.Concat(ConditionalEntries)) {
			if (target is not null)
				yield return target;
		}
	}

	[HarmonyPrefix]
	[HarmonyPriority(Priority.Last)]
	internal static void EnterScope() => ++_scopeCounter;

	[HarmonyFinalizer]
	[HarmonyPriority(Priority.First)]
	internal static void LeaveScope() => --_scopeCounter;

	private static IEnumerable<MethodBase?> GetConditionalEntries() {
		if (AccessTools.TypeByName("SearchAndDestroy.JobGiver_GoWithinRangeOfHostile") is { } type)
			yield return AccessTools.DeclaredMethod(type, "TryGiveJob", [typeof(Pawn)]);
	}
}