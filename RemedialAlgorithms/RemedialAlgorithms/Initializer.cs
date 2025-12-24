using Verse;

namespace TrueMogician.RimWorld.RemedialAlgorithms;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		Settings.Default.Apply();
	}
}