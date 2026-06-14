using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.BattleDossier.Components;
using TrueMogician.RimWorld.BattleDossier.Core;
using Verse;

namespace TrueMogician.RimWorld.BattleDossier.Patches;

/// <summary>
///     Vanilla combat hooks feeding the collector: damage (the bulk of the log) and captures. Downs/kills are
///     captured from battle-log state transitions in <see cref="BattleLogPatches" /> instead.
/// </summary>
internal static class StatsCollectionPatches {
	[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
	[HarmonyPostfix]
	internal static void Thing_TakeDamage_Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result) {
		if (DossierManager.AnySessionActive)
			StatsCollector.OnDamageDealt(__instance, in dinfo, __result);
	}

	[HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.CapturedBy))]
	[HarmonyPostfix]
	internal static void Pawn_GuestTracker_CapturedBy_Postfix(Pawn ___pawn, Faction by, Pawn byPawn) {
		if (DossierManager.AnySessionActive)
			StatsCollector.OnCaptured(___pawn, by, byPawn);
	}
}