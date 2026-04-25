using System;
using System.Globalization;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage;

public static class AmountUtility {
	public const decimal UNSET = -1m;

	public static decimal RawToStock(int count, ThingDef def) {
		if (count <= 0)
			return 0m;
		return count / (decimal)DefCache.StackLimitOf(def);
	}

	public static decimal RawToStock(int count, int stackLimit) {
		if (count <= 0)
			return 0m;
		return count / (decimal)Math.Max(1, stackLimit);
	}

	public static int StockToRawFloor(decimal stock, ThingDef def) {
		if (stock <= 0m)
			return 0;
		return ToInt(Math.Floor(stock * DefCache.StackLimitOf(def)));
	}

	public static int StockToRawCeiling(decimal stock, ThingDef def) {
		if (stock <= 0m)
			return 0;
		return ToInt(Math.Ceiling(stock * DefCache.StackLimitOf(def)));
	}

	public static int StockSlots(decimal stock) {
		if (stock <= 0m)
			return 0;
		return ToInt(Math.Ceiling(stock));
	}

	public static string Format(decimal stock) {
		if (stock < 0m)
			return string.Empty;
		return stock.ToString("0.########", CultureInfo.InvariantCulture);
	}

	public static bool TryParse(string text, out decimal stock) => decimal.TryParse(
		text,
		NumberStyles.AllowDecimalPoint,
		CultureInfo.InvariantCulture,
		out stock
	);

	public static decimal Normalize(decimal stock) => stock < 0m ? UNSET : stock;

	private static int ToInt(decimal value) {
		if (value <= 0m)
			return 0;
		if (value >= int.MaxValue)
			return int.MaxValue;
		return (int)value;
	}
}