using RimWorld;
using Verse;

#pragma warning disable CS8618
// ReSharper disable UnassignedField.Global
// ReSharper disable InconsistentNaming

namespace TrueMogician.RimWorld.Rimsonable.Static;

[DefOf]
public static class Defs {
	public static ThingCategoryDef Grenades;

	public static ThoughtDef Rimsonable_SleptNearPrisoners;

	static Defs() => DefOfHelper.EnsureInitializedInCtor(typeof(Defs));
}