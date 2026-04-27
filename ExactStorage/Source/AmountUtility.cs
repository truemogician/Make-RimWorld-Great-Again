using System;
using System.Globalization;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage;

public static class AmountUtility {
	public const decimal UNSET = -1m;

	public static decimal RawToStock(int count, ThingDef def) =>
		count <= 0 ? 0m : count / (decimal)Math.Max(1, def.stackLimit);

	public static decimal RawToStock(int count, int stackLimit) =>
		count <= 0 ? 0m : count / (decimal)Math.Max(1, stackLimit);

	public static int StockToRawFloor(decimal stock, ThingDef def) =>
		stock <= 0m ? 0 : ToInt(Math.Floor(stock * Math.Max(1, def.stackLimit)));

	public static int StockToRawCeiling(decimal stock, ThingDef def) =>
		stock <= 0m ? 0 : ToInt(Math.Ceiling(stock * Math.Max(1, def.stackLimit)));

	public static int StockSlots(decimal stock) =>
		stock <= 0m ? 0 : ToInt(Math.Ceiling(stock));

	public static string Format(decimal stock) =>
		stock < 0m ? string.Empty : stock.ToString("0.########", CultureInfo.InvariantCulture);

	public static bool TryParse(string text, out decimal stock) => decimal.TryParse(
		text,
		NumberStyles.AllowDecimalPoint,
		CultureInfo.InvariantCulture,
		out stock
	);

	public static decimal Normalize(decimal stock) => stock < 0m ? UNSET : stock;

	private static int ToInt(decimal value) => value switch {
		<= 0m           => 0,
		>= int.MaxValue => int.MaxValue,
		_               => (int)value
	};
}