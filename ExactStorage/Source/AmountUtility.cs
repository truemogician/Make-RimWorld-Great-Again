using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage;

public static class AmountUtility {
	public const decimal UNSET = -1m;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static decimal RawToStack(int count, ThingDef def) => RawToStack(count, def.stackLimit);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static decimal RawToStack(uint count, ThingDef def) => RawToStack(count, def.stackLimit);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static decimal RawToStack(int count, int stackLimit) =>
		count <= 0 ? 0m : count / (decimal)Math.Max(1, stackLimit);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static decimal RawToStack(uint count, int stackLimit) =>
		count == 0u ? 0m : count / (decimal)Math.Max(1, stackLimit);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static uint StackToRaw(decimal stack, ThingDef def, bool floor = false) =>
		ToUInt(stack * Math.Max(1, def.stackLimit), floor);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static uint StackSlots(decimal stack) => ToUInt(stack, false);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Format(decimal stack) =>
		stack < 0m ? string.Empty : stack.ToString("0.########", CultureInfo.InvariantCulture);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryParse(string text, out decimal stack) => decimal.TryParse(
		text,
		NumberStyles.AllowDecimalPoint,
		CultureInfo.InvariantCulture,
		out stack
	);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static decimal Normalize(decimal stack) => stack < 0m ? UNSET : stack;

	private static uint ToUInt(decimal value, bool floor) => value switch {
		<= 0m           => 0u,
		>= uint.MaxValue => uint.MaxValue,
		_               => (uint)(floor ? Math.Floor(value) : Math.Ceiling(value))
	};
}