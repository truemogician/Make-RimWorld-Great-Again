using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable.Static;

[DefOf]
public static class Defs {
	static Defs() {
		DefOfHelper.EnsureInitializedInCtor(typeof(Defs));
	}

	public static ThingCategoryDef Grenades = null!;
}