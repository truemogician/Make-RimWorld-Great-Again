using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace TrueMogician.RimWorld.VehicleFrameworkFixes.Patches;

internal static class PendingPassengerPatches {
	internal const string TRANSLATION_KEY_PREFIX = "VehicleFrameworkFixes.PendingPassenger";

	private const float _CANCEL_BUTTON_SIZE = 24f;

	private const float _PAWN_ROW_HEIGHT = 50f;

	private const float _PAWN_ROW_PADDING = 4f;

	private const float _THING_ICON_SIZE = 27f;

	private const float _LABEL_WIDTH = 100f;

	private static readonly AccessTools.FieldRef<VehiclePawn, List<AssignedSeat>> _boardingAssignmentsRef
		= AccessTools.FieldRefAccess<VehiclePawn, List<AssignedSeat>>("boardingAssignments");

	[HarmonyPatch(typeof(VehicleTabHelper_Passenger), nameof(VehicleTabHelper_Passenger.ListPawns))]
	[HarmonyPostfix]
	internal static void ListPawns_Postfix(
		ref float curY,
		Rect viewRect,
		IThingHolder holder,
		List<Pawn> pawns
	) {
		if (holder is not VehicleRoleHandler handler)
			return;
		if (GetBoardingAssignments(handler.vehicle) is not { } assignments)
			return;
		PurgeStaleAssignments(handler.vehicle, assignments);
		foreach (var seat in assignments) {
			if (seat?.handler != handler || seat.pawn is not { } pawn || pawns.Contains(pawn))
				continue;
			DrawPendingRow(curY, viewRect, handler.vehicle, handler, pawn);
			curY += _PAWN_ROW_HEIGHT;
		}
	}

	[HarmonyPatch(typeof(VehicleTabHelper_Passenger), nameof(VehicleTabHelper_Passenger.GetSize))]
	[HarmonyPostfix]
	internal static void GetSize_Postfix(float paneTopY, ref Vector2 __result) {
		if (Find.Selector.SingleSelectedThing is not VehiclePawn vehicle)
			return;
		if (GetBoardingAssignments(vehicle) is not { } assignments)
			return;
		PurgeStaleAssignments(vehicle, assignments);
		int pendingCount = CountPendingSeats(vehicle, assignments);
		if (pendingCount == 0)
			return;
		__result.y = Mathf.Min(
			ITab_Vehicle_Passengers.WindowHeight,
			Mathf.Min(paneTopY - 30f, __result.y + pendingCount * _PAWN_ROW_HEIGHT)
		);
	}

	[HarmonyPatch(typeof(Pawn_JobTracker), "CleanupCurrentJob")]
	[HarmonyPrefix]
	internal static void CleanupCurrentJob_Prefix(Job? ___curJob, Pawn ___pawn) {
		if (___curJob?.def != JobDefOf_Vehicles.Board)
			return;
		if (___curJob.targetA.Thing is not VehiclePawn vehicle)
			return;
		RemovePendingAssignment(vehicle, ___pawn);
	}

	private static void DrawPendingRow(float curY, Rect viewRect, VehiclePawn vehicle, VehicleRoleHandler handler, Pawn pawn) {
		var rowRect = new Rect(0, curY, viewRect.width, _PAWN_ROW_HEIGHT);
		Widgets.BeginGroup(rowRect);
		var fullRect = rowRect.AtZero();

		var cancelRect = new Rect(
			fullRect.width - _CANCEL_BUTTON_SIZE,
			(fullRect.height - _CANCEL_BUTTON_SIZE) / 2f,
			_CANCEL_BUTTON_SIZE,
			_CANCEL_BUTTON_SIZE
		);
		if (Widgets.ButtonImage(cancelRect, TexButton.Delete, true, $"{TRANSLATION_KEY_PREFIX}.cancelTooltip".Translate(pawn.LabelShortCap))) {
			CancelPending(vehicle, handler, pawn);
			SoundDefOf.Click.PlayOneShotOnCamera();
		}
		fullRect.width -= _CANCEL_BUTTON_SIZE;

		using (new TextBlock(new Color(1f, 1f, 1f, 0.55f))) {
			var iconRect = new Rect(_PAWN_ROW_PADDING, (rowRect.height - _THING_ICON_SIZE) / 2f, _THING_ICON_SIZE, _THING_ICON_SIZE);
			Widgets.ThingIcon(iconRect, pawn);
			var labelRect = new Rect(iconRect.xMax + _PAWN_ROW_PADDING, 16f, _LABEL_WIDTH, 18f);
			GenMapUI.DrawPawnLabel(pawn, labelRect, 1f, _LABEL_WIDTH, null, GameFont.Small, false, false);
		}

		using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.6f))) {
			var statusRect = new Rect(
				_PAWN_ROW_PADDING + _THING_ICON_SIZE + _PAWN_ROW_PADDING + _LABEL_WIDTH + _PAWN_ROW_PADDING,
				0,
				fullRect.width,
				rowRect.height
			);
			Widgets.Label(statusRect, $"{TRANSLATION_KEY_PREFIX}.statusLabel".Translate());
		}

		var tooltipRect = new Rect(0, 0, fullRect.width, rowRect.height);
		if (Mouse.IsOver(tooltipRect))
			TooltipHandler.TipRegion(tooltipRect, $"{TRANSLATION_KEY_PREFIX}.rowTooltip".Translate(pawn.LabelShortCap, handler.role.label));

		Widgets.EndGroup();
	}

	private static void CancelPending(VehiclePawn vehicle, VehicleRoleHandler handler, Pawn pawn) {
		RemovePendingAssignment(vehicle, pawn, handler);
		if (pawn.jobs is { } jobs) {
			if (IsBoardJobFor(jobs.curJob, vehicle))
				jobs.EndCurrentJob(JobCondition.InterruptForced);
			jobs.jobQueue?.RemoveAll(pawn, job => IsBoardJobFor(job, vehicle));
		}
		if (vehicle.Map?.GetCachedMapComponent<VehicleReservationManager>() is { } resMgr)
			resMgr.ReleaseAllClaimedBy(pawn);
	}

	private static List<AssignedSeat>? GetBoardingAssignments(VehiclePawn? vehicle) {
		if (vehicle is null)
			return null;
		var assignments = _boardingAssignmentsRef(vehicle);
		return assignments.NullOrEmpty() ? null : assignments;
	}

	private static int CountPendingSeats(VehiclePawn vehicle, List<AssignedSeat> assignments) {
		var pendingCount = 0;
		foreach (var handler in vehicle.handlers)
			pendingCount += CountPendingSeats(handler, assignments);
		return pendingCount;
	}

	private static int CountPendingSeats(VehicleRoleHandler handler, List<AssignedSeat> assignments) {
		var pendingCount = 0;
		foreach (var seat in assignments) {
			if (seat?.handler == handler && seat.pawn is not null)
				pendingCount++;
		}
		return pendingCount;
	}

	private static void PurgeStaleAssignments(VehiclePawn vehicle, List<AssignedSeat> assignments) {
		for (int i = assignments.Count - 1; i >= 0; i--) {
			var seat = assignments[i];
			if (!IsPendingSeatValid(vehicle, seat))
				assignments.RemoveAt(i);
		}
	}

	private static bool IsPendingSeatValid(VehiclePawn vehicle, AssignedSeat? seat) {
		if (seat?.pawn is not { } pawn || seat.handler?.vehicle != vehicle)
			return false;
		if (seat.handler.thingOwner.Contains(pawn))
			return false;
		if (pawn.jobs is not { } jobs)
			return false;
		if (IsBoardJobFor(jobs.curJob, vehicle))
			return true;
		if (HasBoardJobQueued(jobs, vehicle))
			return true;
		if (vehicle.Map?.GetCachedMapComponent<VehicleReservationManager>()?.GetReservation<VehicleHandlerReservation>(vehicle)
			is { } reservation)
			return reservation.ReservedHandler(pawn) == seat.handler;
		return false;
	}

	private static bool HasBoardJobQueued(Pawn_JobTracker jobs, VehiclePawn vehicle) {
		if (jobs.jobQueue is not { } jobQueue)
			return false;
		foreach (var job in jobQueue) {
			if (IsBoardJobFor(job.job, vehicle))
				return true;
		}
		return false;
	}

	private static bool IsBoardJobFor(Job? job, VehiclePawn vehicle)
		=> job?.def == JobDefOf_Vehicles.Board && job.targetA.Thing == vehicle;

	private static void RemovePendingAssignment(VehiclePawn vehicle, Pawn pawn, VehicleRoleHandler? handler = null) {
		if (GetBoardingAssignments(vehicle) is not { } assignments)
			return;
		for (int i = assignments.Count - 1; i >= 0; i--) {
			var seat = assignments[i];
			if (seat?.pawn == pawn && (handler is null || seat.handler == handler))
				assignments.RemoveAt(i);
		}
	}
}