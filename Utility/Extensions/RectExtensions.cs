using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

		public Rect Padding(float top, float right, float bottom, float left) => new(
			rect.xMin + left,
			rect.yMin + top,
			rect.width - left - right,
			rect.height - top - bottom
		);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Rect Padding(float top, float leftRight, float bottom) => rect.Padding(top, leftRight, bottom, leftRight);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Rect Padding(float topBottom, float leftRight) => rect.Padding(topBottom, leftRight, topBottom, leftRight);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Rect Padding(float padding) => rect.Padding(padding, padding, padding, padding);
	}
}