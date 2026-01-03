using RimWorld;
using Verse;

#pragma warning disable CS8618
// ReSharper disable UnassignedField.Global

namespace TrueMogician.RimWorld.Rimsonable.Static;

[DefOf]
public static class Defs {
	public static ThingCategoryDef Grenades;

	static Defs() => DefOfHelper.EnsureInitializedInCtor(typeof(Defs));
}