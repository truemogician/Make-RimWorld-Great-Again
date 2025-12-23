using TrueMogician.RimWorld.Utility;

namespace TrueMogician.RimWorld.Profiler;

public static class Helper {
	public static readonly Logger Logger = new(ThisAssembly.Info.Title) { Enabled = true };
}