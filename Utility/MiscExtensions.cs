using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Verse;

namespace TrueMogician.RimWorld.Utility;

public static class MapExtensions {
	extension(Map self) {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IntVec3 IndexToCell(int index) => CellIndicesUtility.IndexToCell(index, self.Size.x);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CellToIndex(IntVec3 cell) => CellIndicesUtility.CellToIndex(cell, self.Size.x);
	}
}