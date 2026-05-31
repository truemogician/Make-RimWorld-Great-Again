using System;
using Verse;

namespace TrueMogician.RimWorld.RemedialAlgorithms.Components;

internal static class HediffDirtyHub {
	internal static event Action<Pawn, Map>? PawnDespawnedFromMap;

	internal static event Action<Pawn>? PawnHediffsDirtied;

	internal static event Action<Pawn, Map>? PawnSpawnedOnMap;

	internal static void OnHediffsDirtied(Pawn? pawn) {
		if (pawn != null)
			PawnHediffsDirtied?.Invoke(pawn);
	}

	internal static void OnPawnSpawned(Pawn? pawn, Map? map) {
		if (pawn != null && map != null)
			PawnSpawnedOnMap?.Invoke(pawn, map);
	}

	internal static void OnPawnDespawned(Pawn? pawn, Map? map) {
		if (pawn != null && map != null)
			PawnDespawnedFromMap?.Invoke(pawn, map);
	}
}