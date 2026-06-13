using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.FlippedBuildings.Core;

public static class MirrorTransform {
	// Even width has no center column: GenAdj.OccupiedRect anchors at center.x-(w-1)/2, so the axis is +0.5 and x maps to 1-x.
	public static IntVec3 MirrorCellOffset(IntVec3 offset, IntVec2 size) {
		int x = size.x % 2 == 0 ? 1 - offset.x : -offset.x;
		return new IntVec3(x, offset.y, offset.z);
	}

	public static Vector3 MirrorDrawOffset(Vector3 offset) => new(-offset.x, offset.y, offset.z);

	public static Rot4 MirrorRotation(Rot4 rot) => rot.IsHorizontal ? rot.Opposite : rot;

	public static bool IsAsymmetric(IntVec3 offset, IntVec2 size) => MirrorCellOffset(offset, size) != offset;
}