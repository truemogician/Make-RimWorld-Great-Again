using System.Diagnostics;
using System.Runtime.CompilerServices;
using TrueMogician.RimWorld.Utility;

namespace TrueMogician.RimWorld.Profiler;

public static class Helper {
	public static readonly Logger Logger = new(ThisAssembly.Info.Title) { Enabled = true };

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static double TickToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static double TickToMs(double ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}