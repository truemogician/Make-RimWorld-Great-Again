using RimWorld;

namespace TrueMogician.RimWorld.BattleDossier.Static;

[DefOf]
public static class BattleDossierDefOf {
	public static MainButtonDef BattleDossier = null!;

	static BattleDossierDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(BattleDossierDefOf));
}