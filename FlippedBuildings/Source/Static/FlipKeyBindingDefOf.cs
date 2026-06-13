using RimWorld;
using Verse;

// ReSharper disable InconsistentNaming

namespace TrueMogician.RimWorld.FlippedBuildings.Static;

[DefOf]
public static class FlipKeyBindingDefOf {
	public static KeyBindingDef FlippedBuildings_Flip = null!;

	static FlipKeyBindingDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(FlipKeyBindingDefOf));
}