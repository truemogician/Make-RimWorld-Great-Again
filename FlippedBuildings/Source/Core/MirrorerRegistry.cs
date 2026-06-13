using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.FlippedBuildings.Core;

public readonly struct MirrorContext(IntVec2 size) {
	public IntVec2 Size { get; } = size;

	public IntVec3 MirrorCell(IntVec3 offset) => MirrorTransform.MirrorCellOffset(offset, Size);
}

public delegate void CompMirrorer(CompProperties props, MirrorContext context);

public delegate bool CompAsymmetryProbe(CompProperties props, IntVec2 size);

// Per-CompProperties mirrorers for geometry the generic GraphicData mirroring cannot reach (typically cell
// offsets). Runs on a clone, so mutation is safe. An optional probe makes the geometry count toward eligibility.
public static class MirrorerRegistry {
	private static readonly Dictionary<Type, CompMirrorer> _mirrorers = new();

	private static readonly Dictionary<Type, CompAsymmetryProbe> _probes = new();

	static MirrorerRegistry() =>
		Register(
			typeof(CompProperties_ThingContainer),
			(props, ctx) => {
				var p = (CompProperties_ThingContainer)props;
				p.containedThingOffset = ctx.MirrorCell(p.containedThingOffset);
			}
		);

	public static void Register(Type compPropsType, CompMirrorer mirrorer, CompAsymmetryProbe? asymmetryProbe = null) {
		_mirrorers[compPropsType] = mirrorer;
		if (asymmetryProbe != null)
			_probes[compPropsType] = asymmetryProbe;
	}

	public static bool HasMirrorer(Type compPropsType) => _mirrorers.ContainsKey(compPropsType);

	public static void ApplyTo(CompProperties clone, MirrorContext context) {
		if (!_mirrorers.TryGetValue(clone.GetType(), out var mirrorer))
			return;
		try {
			mirrorer(clone, context);
		}
		catch (Exception ex) {
			Helper.Logger.Error($"Mirrorer for {clone.GetType().Name} threw: {ex}");
		}
	}

	public static bool AnyAsymmetric(ThingDef def) {
		if (def.comps is not { Count: > 0 })
			return false;
		foreach (var cp in def.comps) {
			if (_probes.TryGetValue(cp.GetType(), out var probe) && probe(cp, def.size))
				return true;
		}
		return false;
	}
}