using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TrueMogician.RimWorld.Utility.Extensions;
using TrueMogician.RimWorld.Utility.GUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrueMogician.RimWorld.Utility.Extensions;

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

		public Flexbox ToFlexbox(
			FlexDirection direction,
			int count,
			float gap = 0f,
			JustifyContent justifyContent = JustifyContent.FlexStart
		) => new(rect, Enumerable.Repeat(Flexbox.Length.Auto, count).ToArray()) {
			Direction = direction,
			Gap = gap,
			JustifyContent = justifyContent
		};
	}
}