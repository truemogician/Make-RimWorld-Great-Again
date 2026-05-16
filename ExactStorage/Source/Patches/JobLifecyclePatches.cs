using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Utility.Diagnostics;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.ExactStorage.Patches;

/// <summary>
///     Diagnostic-only patches that record HaulToCell job lifecycle events. Replaces the vanilla
///     <c>Prefs.DevMode</c>-gated <c>jobsGivenThisTickTextual</c> string so we can correlate the job loop with
///     ExactStorage's <c>Allows</c> / <c>EnrouteStock</c> trace without flipping vanilla dev settings.
/// </summary>
internal static class JobLifecyclePatches {
	private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> _pawnRef =
		AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

	[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
	[HarmonyPrefix]
	internal static void Pawn_JobTracker_StartJob_Prefix(
		Pawn_JobTracker __instance,
		Job? newJob,
		JobCondition lastJobEndCondition,
		ThinkNode? jobGiver
	) {
		if (Diagnostic.Level == Verbosity.Off || newJob?.def != JobDefOf.HaulToCell)
			return;
		var pawn = _pawnRef(__instance);
		var thing = newJob.GetTarget(TargetIndex.A).Thing;
		var cell = newJob.GetTarget(TargetIndex.B).Cell;
		var details = $"last={lastJobEndCondition}\tgiver={jobGiver?.GetType().Name ?? "?"}\tcount={newJob.count}";
		Diagnostic.Record("JobStart", "HaulToCell", pawn, thing, cell, details);
	}

	[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
	[HarmonyPrefix]
	internal static void Pawn_JobTracker_EndCurrentJob_Prefix(Pawn_JobTracker __instance, JobCondition condition, bool startNewJob) {
		if (Diagnostic.Level == Verbosity.Off)
			return;
		var curJob = __instance.curJob;
		if (curJob?.def != JobDefOf.HaulToCell)
			return;
		var pawn = _pawnRef(__instance);
		var thing = curJob.GetTarget(TargetIndex.A).Thing;
		var cell = curJob.GetTarget(TargetIndex.B).Cell;
		int toilIdx = __instance.curDriver?.CurToilIndex ?? -1;
		var details = $"cond={condition}\tstartNew={startNewJob}\ttoil={toilIdx}";
		Diagnostic.Record("JobEnd", "HaulToCell", pawn, thing, cell, details);
	}
}