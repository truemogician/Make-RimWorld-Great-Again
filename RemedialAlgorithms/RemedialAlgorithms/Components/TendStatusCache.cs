using System.Runtime.CompilerServices;
using Verse;

namespace TrueMogician.RimWorld.RemedialAlgorithms.Components;

/**
 * Per-pawn, per-tick memoization for HealthAIUtility.ShouldBeTendedNowByPlayer.
 */
internal static class TendStatusCache {
	private static readonly ConditionalWeakTable<Pawn, Entry> _entries = [];

	private static bool _active;

	internal static void Activate() {
		_active = true;
		HediffDirtyHub.PawnHediffsDirtied += Invalidate;
	}

	internal static void Deactivate() {
		_active = false;
		HediffDirtyHub.PawnHediffsDirtied -= Invalidate;
		_entries.Clear();
	}

	internal static bool TryGetNeedsTending(Pawn pawn, out bool result) {
		result = false;
		if (!_active)
			return false;
		if (!_entries.TryGetValue(pawn, out var entry))
			return false;
		if (entry.NeedsTendingTick != Find.TickManager.TicksGame)
			return false;
		result = entry.NeedsTending;
		return true;
	}

	internal static bool TryGetNeedsUrgentTending(Pawn pawn, out bool result) {
		result = false;
		if (!_active)
			return false;
		if (!_entries.TryGetValue(pawn, out var entry))
			return false;
		if (entry.NeedsUrgentTendingTick != Find.TickManager.TicksGame)
			return false;
		result = entry.NeedsUrgentTending;
		return true;
	}

	internal static void StoreNeedsTending(Pawn pawn, bool value) {
		if (!_active)
			return;
		var entry = _entries.GetValue(pawn, _ => new Entry());
		entry.NeedsTending = value;
		entry.NeedsTendingTick = Find.TickManager.TicksGame;
	}

	internal static void StoreNeedsUrgentTending(Pawn pawn, bool value) {
		if (!_active)
			return;
		var entry = _entries.GetValue(pawn, _ => new Entry());
		entry.NeedsUrgentTending = value;
		entry.NeedsUrgentTendingTick = Find.TickManager.TicksGame;
	}

	private static void Invalidate(Pawn pawn) {
		if (_entries.TryGetValue(pawn, out var entry)) {
			entry.NeedsTendingTick = -1;
			entry.NeedsUrgentTendingTick = -1;
		}
	}

	private sealed class Entry {
		internal bool NeedsTending;

		internal int NeedsTendingTick = -1;

		internal bool NeedsUrgentTending;

		internal int NeedsUrgentTendingTick = -1;
	}
}