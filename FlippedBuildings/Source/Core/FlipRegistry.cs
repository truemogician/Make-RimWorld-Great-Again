using System.Collections.Generic;
using Verse;

namespace TrueMogician.RimWorld.FlippedBuildings.Core;

public sealed class FlipCandidate(ThingDef def) {
	public ThingDef Canonical { get; } = def;

	public string DefName => Canonical.defName;

	public string Label => Canonical.label;

	public string ModName => Canonical.modContentPack?.Name ?? "Core";
}

public static class FlipRegistry {
	private static readonly Dictionary<ThingDef, ThingDef> _canonicalToFlipped = new();

	private static readonly Dictionary<ThingDef, ThingDef> _flippedToCanonical = new();

	private static readonly List<FlipCandidate> _candidates = [];

	public static IReadOnlyList<FlipCandidate> Candidates => _candidates;

	public static void Clear() {
		_canonicalToFlipped.Clear();
		_flippedToCanonical.Clear();
		_candidates.Clear();
	}

	public static void RecordCandidate(ThingDef canonical) => _candidates.Add(new FlipCandidate(canonical));

	public static void Register(ThingDef canonical, ThingDef flipped) {
		_canonicalToFlipped[canonical] = flipped;
		_flippedToCanonical[flipped] = canonical;
	}

	public static bool IsFlipped(ThingDef def) => _flippedToCanonical.ContainsKey(def);

	public static ThingDef? GetFlipped(ThingDef def) => _canonicalToFlipped.GetValueOrDefault(def);

	public static ThingDef? GetCanonical(ThingDef def) => _flippedToCanonical.GetValueOrDefault(def);

	public static ThingDef? GetTwin(ThingDef def) => GetFlipped(def) ?? GetCanonical(def);

	public static ThingDef Canonicalize(ThingDef def) => GetCanonical(def) ?? def;
}