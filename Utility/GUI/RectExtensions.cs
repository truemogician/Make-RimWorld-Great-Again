using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrueMogician.RimWorld.Utility.GUI;

public static class RectExtensions {
	extension(Rect rect) {
		public Flexbox ToFlexbox(
			FlexDirection direction,
			IEnumerable<Flexbox.Length> lengths,
			float gap = 0f,
			JustifyContent justifyContent = JustifyContent.FlexStart
		) => new(rect, lengths.ToArray()) {
			Direction = direction,
			Gap = gap,
			JustifyContent = justifyContent
		};

		public Flexbox ToFlexbox(
			IEnumerable<Flexbox.Length> lengths,
			float gap = 0f,
			JustifyContent justifyContent = JustifyContent.FlexStart
		) => new(rect, lengths.ToArray()) {
			Gap = gap,
			JustifyContent = justifyContent
		};
	}
}