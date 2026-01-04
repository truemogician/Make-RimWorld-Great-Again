using System;
using System.Linq;
using System.Reflection;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable.CE.Patches;

public static class TargetMarkEnhancements {
	public const float ACCURACY_BONUS = 0.10f;

	private const string _MARKER_TYPE_NAME = "CombatExtended.ArtilleryMarker";

	private static string? markerDefName;
	private static ThingDef? markerDef;

	private static Type? markerType;
	private static FieldInfo? markerCasterField;

	private static readonly FieldInfo? AccuracyFactorIntField =
		AccessTools.Field(typeof(ShiftVecReport), "accuracyFactorInt");

	private static string MarkerDefName => markerDefName ??= ResolveMarkerDefName();

	private static ThingDef MarkerDef => markerDef ??= ThingDef.Named(MarkerDefName);

	[HarmonyPatch(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.TryFindNewTarget))]
	[HarmonyPostfix]
	public static void Building_TurretGunCE_TryFindNewTarget_Postfix(Building_TurretGunCE __instance, ref LocalTargetInfo __result) {
		if (__instance is not { Spawned: true, Map: { } map })
			return;
		if (!__instance.IsMortarOrProjectileFliesOverhead)
			return;
		if (HasArtilleryMarker(__result, map))
			return;
		if (__instance.Faction is not { } faction)
			return;
		if (__instance.AttackVerb is not { } verb || verb.verbProps?.range is not { } maxRange || maxRange <= 0f)
			return;

		var markers = map.listerThings.ThingsOfDef(MarkerDef);
		if (markers is null || markers.Count == 0)
			return;

		var bestTarget = __result;
		float bestScore = float.NegativeInfinity;
		foreach (var marker in markers.OfType<AttachableThing>()) {
			var caster = GetMarkerCaster(marker);
			if (caster?.Faction != faction)
				continue;
			var markedThing = marker.parent;
			if (!Settings.Default.AutoTargetMarksOnNonHostile && (markedThing is null || !markedThing.HostileTo(faction)))
				continue;
			var target = markedThing ?? (LocalTargetInfo)marker.Position;
			float dist = (target.Cell - __instance.Position).LengthHorizontal;
			if (dist > maxRange)
				continue;
			if (!verb.CanHitTargetFrom(__instance.Position, target))
				continue;
			float score = maxRange - dist;
			if (score > bestScore) {
				bestScore = score;
				bestTarget = target;
			}
		}

		if (!float.IsNegativeInfinity(bestScore))
			__result = bestTarget;
	}

	[HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.ShiftVecReportFor), typeof(LocalTargetInfo), typeof(IntVec3))]
	[HarmonyPostfix]
	public static void Verb_LaunchProjectileCE_ShiftVecReportFor_Postfix(
		Verb_LaunchProjectileCE __instance,
		LocalTargetInfo target,
		IntVec3 targetCell,
		ref ShiftVecReport? __result
	) {
		if (__result is null)
			return;
		if (__instance.caster is not Building_TurretGunCE turret || turret.Map is null)
			return;
		if (turret.IsMortarOrProjectileFliesOverhead)
			return;
		if (!HasArtilleryMarker(target, turret.Map))
			return;
		__result.aimingAccuracy = Math.Min(1.5f, __result.aimingAccuracy + ACCURACY_BONUS);
		AccuracyFactorIntField?.SetValue(__result, -1f);
	}

	private static string ResolveMarkerDefName() {
		// Fallback is correct even if reflection fails.
		const string fallback = "ArtilleryMarker";
		markerType ??= AccessTools.TypeByName(_MARKER_TYPE_NAME);
		var defField = markerType?.GetField("MarkerDef", BindingFlags.Public | BindingFlags.Static);
		if (defField is null)
			return fallback;
		return defField.GetRawConstantValue() as string ?? fallback;
	}

	private static Pawn? GetMarkerCaster(Thing markerThing) {
		markerType ??= AccessTools.TypeByName(_MARKER_TYPE_NAME);
		if (markerType is null || !markerType.IsInstanceOfType(markerThing))
			return null;
		markerCasterField ??= markerType.GetField(
			"caster",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
		);
		return markerCasterField?.GetValue(markerThing) as Pawn;
	}

	private static bool HasArtilleryMarker(LocalTargetInfo target, Map map) {
		if (!target.IsValid)
			return false;
		if (target.HasThing)
			return target.Thing.HasAttachment(MarkerDef);
		return target.Cell.InBounds(map) && target.Cell.GetFirstThing(map, MarkerDef) is not null;
	}
}