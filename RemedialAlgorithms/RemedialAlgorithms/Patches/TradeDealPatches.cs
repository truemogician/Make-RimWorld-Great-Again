using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

// ReSharper disable InconsistentNaming

namespace TrueMogician.RimWorld.RemedialAlgorithms.Patches;

[HarmonyPatch(typeof(TradeDeal))]
public static class TradeDealPatches {
	private static readonly Dictionary<TradeableKey, List<Tradeable>> _tradeables = [];

	private static readonly FieldInfo? _tradeablesField = AccessTools.Field(typeof(TradeDeal), "tradeables");

	private static List<Tradeable>? _originalTradeables;

	private static bool _enabled;

	[HarmonyPatch("AddAllTradeables")]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPrefix]
	public static void AddAllTradeables_Prefix(TradeDeal __instance) {
		if (_tradeablesField?.GetValue(__instance) is not List<Tradeable> list)
			_enabled = false;
		else {
			_originalTradeables = list;
			_enabled = true;
		}
	}

	[HarmonyPatch("AddAllTradeables")]
	[HarmonyFinalizer]
	public static void AddAllTradeables_Postfix() {
		_tradeables.Clear();
		_originalTradeables = null;
		_enabled = false;
	}

	[HarmonyPatch("AddToTradeables")]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPrefix]
	public static bool AddToTradeables_Prefix(Thing? t, Transactor trans) {
		if (!_enabled)
			return true; // Fallback to original method
		if (t is null)
			return false;
		if (Match(t) is not { } tradeable) {
			tradeable = t is Pawn ? new Tradeable_Pawn() : new Tradeable();
			var key = new TradeableKey(t);
			if (!_tradeables.TryGetValue(key, out var list))
				_tradeables[key] = list = [];
			list.Add(tradeable);
			// Note: batch update in AddAllTradeables_Postfix is more optimal, but may break compatibility with other mods.
			_originalTradeables!.Add(tradeable);
		}
		tradeable.AddThing(t, trans);
		return false; // Always skip original method
	}

	private static Tradeable? Match(Thing thing) {
		if (!_tradeables.TryGetValue(thing, out var list))
			return null;
		foreach (var t in list) {
			var mode = t.TraderWillTrade ? TransferAsOneMode.Normal : TransferAsOneMode.InactiveTradeable;
			if (TransferableUtility.TransferAsOne(thing, t.AnyThing, mode))
				return t;
		}
		return null;
	}
}

internal readonly struct TradeableKey : IEquatable<TradeableKey> {
	public TradeableKey(Thing thing) {
		var inner = thing.GetInnerIfMinified()!;
		Minified = !ReferenceEquals(thing, inner);
		Def = inner.def;
		StuffDef = inner.Stuff;
	}

	public TradeableKey(Tradeable tradeable)
		: this(tradeable.AnyThing ?? throw new ArgumentException("Invalid tradeable", nameof(tradeable))) { }

	public bool Minified { get; }

	public ThingDef Def { get; }

	public ThingDef? StuffDef { get; }

	public static implicit operator TradeableKey(Thing thing) => new(thing);

	public static implicit operator TradeableKey(Tradeable tradeable) => new(tradeable);

	public bool Equals(TradeableKey other) => Minified == other.Minified && ReferenceEquals(Def, other.Def) && Equals(StuffDef, other.StuffDef);

	public override bool Equals(object? obj) => obj is TradeableKey other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(Minified, Def, StuffDef);
}