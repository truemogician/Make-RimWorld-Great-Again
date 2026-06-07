using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using SmashTools;
using TacticalGroups;
using UnityEngine;
using Vehicles;
using Verse;

namespace TrueMogician.RimWorld.VehicleFrameworkFixes.CG.Patches;

/// <summary>
///     Colony Groups replaces the vanilla colonist bar with its own <see cref="TacticalColonistBar" />, which gathers
///     colonists from <see cref="MapPawns.FreeColonists" />. Pawns aboard a vehicle are de-spawned, so they never appear.
///     Vehicle Framework solves this for the vanilla bar by patching <c>ColonistBar</c> directly; those patches don't
///     reach Colony Groups' parallel implementation. This restores both behaviours on the Colony Groups bar.
/// </summary>
[HarmonyPatch]
internal static class ColonistBarPatches {
	private const float _VEHICLE_ICON_SIZE = 20f;

	private static readonly FieldInfo _tmpPawnsField = AccessTools.Field(typeof(TacticalColonistBar), "tmpPawns");

	private static readonly FieldInfo _tmpMapsField = AccessTools.Field(typeof(TacticalColonistBar), "tmpMaps");

	private static readonly MethodInfo _freeColonistsGetter = AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.FreeColonists));

	private static readonly MethodInfo _addRangeMethod = AccessTools.Method(typeof(List<Pawn>), nameof(List<>.AddRange));

	private static readonly MethodInfo _mapListGetItem = AccessTools.PropertyGetter(typeof(List<Map>), "Item");

	private static readonly MethodInfo _addInVehicleColonistsMethod = AccessTools.Method(typeof(ColonistBarPatches), nameof(AddInVehicleColonists));

	private static readonly MethodInfo _thingMapGetter = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map));

	private static readonly MethodInfo _colonistBarMapMethod = AccessTools.Method(typeof(ColonistBarPatches), nameof(GetColonistBarMap));

	/// <summary>
	///     Inject player colonists aboard vehicles into the per-map pawn list, right after the <c>FreeColonists</c> are
	///     added. They then flow through Colony Groups' existing grouping, sorting and layout pipeline unchanged.
	/// </summary>
	[HarmonyPatch(typeof(TacticalColonistBar), "CheckRecacheEntries")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> CheckRecacheEntries_Transpiler(IEnumerable<CodeInstruction> instructions) {
		var code = instructions.ToList();
		int getterIdx = code.FindIndex(ci => ci.Calls(_freeColonistsGetter));
		int addRangeIdx = getterIdx < 0 ? -1 : code.FindIndex(getterIdx + 1, ci => ci.Calls(_addRangeMethod));
		int tmpMapsIdx = getterIdx < 0 ? -1 : code.FindLastIndex(getterIdx, ci => ci.LoadsField(_tmpMapsField));
		int getItemIdx = tmpMapsIdx < 0 ? -1 : code.FindIndex(tmpMapsIdx + 1, ci => ci.Calls(_mapListGetItem));
		if (addRangeIdx < 0 || tmpMapsIdx < 0 || getItemIdx < 0 || getItemIdx >= addRangeIdx) {
			Helper.Logger.Error("Could not locate the FreeColonists injection point in TacticalColonistBar.CheckRecacheEntries.");
			return code;
		}
		// Clone the method's own `tmpMaps[i]` access (ldsfld tmpMaps … callvirt get_Item).
		// The loop index may be a closure field rather than a simple local, so reproducing the exact instructions is safer than rebuilding it.
		List<CodeInstruction> injection = [new(OpCodes.Ldsfld, _tmpPawnsField)];
		for (int i = tmpMapsIdx; i <= getItemIdx; i++)
			injection.Add(new CodeInstruction(code[i].opcode, code[i].operand));
		injection.Add(new CodeInstruction(OpCodes.Call, _addInVehicleColonistsMethod));
		code.InsertRange(addRangeIdx + 1, injection);
		return code;
	}

	/// <summary>
	///     Colonists aboard a vehicle are de-spawned, so <see cref="Thing.Map" /> returns <see langword="null" /> and Colony
	///     Groups' "hide pawns when off map" filter would drop them. Redirect those lookups to the map of the vehicle they
	///     are in, so they count as present on that map.
	/// </summary>
	[HarmonyPatch(typeof(TacticalColonistBar), "GetNonHiddenPawns")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> GetNonHiddenPawns_Transpiler(IEnumerable<CodeInstruction> instructions) {
		var replaced = 0;
		foreach (var inst in instructions) {
			if (inst.Calls(_thingMapGetter)) {
				inst.opcode = OpCodes.Call;
				inst.operand = _colonistBarMapMethod;
				replaced++;
			}
			yield return inst;
		}
		if (replaced == 0)
			Helper.Logger.Error("Could not find any Pawn.Map lookups in TacticalColonistBar.GetNonHiddenPawns.");
	}

	/// <summary>
	///     Draw the vehicle activity badge on portraits of colonists currently aboard a vehicle, mirroring Vehicle
	///     Framework's <c>DrawIconsVehicles</c> postfix which only targets the vanilla colonist bar drawer.
	/// </summary>
	[HarmonyPatch(typeof(TacticalGroups_ColonistBarColonistDrawer), nameof(TacticalGroups_ColonistBarColonistDrawer.DrawColonist))]
	[HarmonyPostfix]
	internal static void DrawColonist_Postfix(Rect rect, Pawn? colonist) {
		if (colonist is null or { Dead: true } || colonist.ParentHolder is not VehicleRoleHandler handler)
			return;
		if (!VehicleTex.CachedTextureIcons.TryGetValue(handler.vehicle.VehicleDef, out var icon) || !icon)
			return;
		float size = _VEHICLE_ICON_SIZE * TacticUtils.TacticalColonistBar.Scale;
		var iconRect = new Rect(rect.xMax - size - 1f, rect.yMax - size - 1f, size, size);
		GUI.DrawTexture(iconRect, icon);
		TooltipHandler.TipRegion(iconRect, "VF_ActivityIconOnBoardShip".Translate(handler.vehicle.Label));
	}

	private static void AddInVehicleColonists(List<Pawn> pawns, Map map) {
		if (map.GetDetachedMapComponent<VehiclePositionManager>() is not { } positionManager)
			return;
		foreach (var vehicle in positionManager.AllClaimants) {
			if (vehicle.Faction != Faction.OfPlayer)
				continue;
			foreach (var pawn in vehicle.AllPawnsAboard) {
				if (pawn is { IsColonist: true })
					pawns.Add(pawn);
			}
		}
	}

	private static Map? GetColonistBarMap(Pawn pawn) {
		if (pawn.Map is { } map)
			return map;
		return pawn.ParentHolder is { } holder ? holder.GetVehicle()?.Map : null;
	}
}