using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TrueMogician.RimWorld.Utility.GUI;

public static class RectExtensions {
	extension(Rect rect) {
		public Flexbox ToFlexbox(IEnumerable<Flexbox.Length> lengths, float gap = 0f)
			=> new(rect, lengths.ToArray()) { Gap = gap };
	}
}