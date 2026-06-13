using Verse;

// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global

namespace TrueMogician.RimWorld.FlippedBuildings.Defs;

public class FlipSpec : DefModExtension {
	// null = auto-detect, true = force a twin, false = suppress.
	public bool? allow;

	// Pre-made mirrored texture; replaces UV-flip rendering for this def.
	public string? mirroredTexturePath;
}