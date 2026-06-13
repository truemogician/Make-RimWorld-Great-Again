using System;
using System.Collections.Generic;
using Verse;

namespace TrueMogician.RimWorld.FlippedBuildings.Core;

// Asymmetry that lives in code, not a def field (e.g. the pod launcher's fueling port, computed in FuelingPortUtility).
// Not detectable generically, so each case registers a predicate and is mirrored by a patch.
public static class SpecialAsymmetry {
	private static readonly List<Func<ThingDef, bool>> _detectors = [];

	static SpecialAsymmetry() => Register(def => def.building is { hasFuelingPort: true });

	public static void Register(Func<ThingDef, bool> detector) => _detectors.Add(detector);

	public static bool IsAsymmetric(ThingDef def) {
		foreach (var detector in _detectors) {
			if (detector(def))
				return true;
		}
		return false;
	}
}