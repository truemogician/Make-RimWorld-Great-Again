using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrueMogician.RimWorld.Utility.GUI;

public class Flexbox(Rect rect, params Flexbox.Length[] lengths) : IEnumerable<Rect> {
	public IEnumerator<Rect> GetEnumerator() {
		bool isRow = Direction is FlexDirection.Row or FlexDirection.RowReverse;
		bool isReverse = Direction is FlexDirection.RowReverse or FlexDirection.ColumnReverse;
		float mainAxisSize = isRow ? rect.width : rect.height;
		string mainAxisName = isRow ? "width" : "height";

		int count = Lengths.Count;
		float totalFixedPx = Lengths.Sum(l => l.Px);
		float totalFr = Lengths.Sum(l => l.Fr);
		float totalGapPx = Gap * (count - 1);
		float totalFixedAndGapPx = totalFixedPx + totalGapPx;

		if (totalFixedAndGapPx > mainAxisSize) {
			throw new ArgumentOutOfRangeException(
				nameof(Lengths),
				$"Total fixed {mainAxisName} + gaps ({totalFixedAndGapPx}px) exceeds the available Rect {mainAxisName} ({mainAxisSize}px)."
			);
		}

		float availableSpace = mainAxisSize - totalFixedAndGapPx;
		float pxPerFr = totalFr > 0 ? availableSpace / totalFr : 0f;
		float freeSpace = totalFr > 0 ? 0f : availableSpace;

		var startOffset = 0f;
		var extraBetween = 0f;
		if (freeSpace > 0f) {
			switch (JustifyContent) {
				case JustifyContent.FlexStart: break;
				case JustifyContent.FlexEnd:   startOffset = freeSpace; break;
				case JustifyContent.Center:    startOffset = freeSpace / 2f; break;
				case JustifyContent.SpaceBetween:
					if (count > 1)
						extraBetween = freeSpace / (count - 1);
					break;
				case JustifyContent.SpaceAround:
					extraBetween = freeSpace / count;
					startOffset = extraBetween / 2f;
					break;
				case JustifyContent.SpaceEvenly:
					extraBetween = freeSpace / (count + 1);
					startOffset = extraBetween;
					break;
				default: throw new ArgumentOutOfRangeException(nameof(JustifyContent), JustifyContent, "Unsupported justify content.");
			}
		}

		float effectiveGap = Gap + extraBetween;
		float mainAxisStart = isRow ? rect.x : rect.y;
		float mainAxisEnd = mainAxisStart + mainAxisSize;

		if (!isReverse) {
			float current = mainAxisStart + startOffset;
			for (var i = 0; i < count; i++) {
				float len = Lengths[i].CalculatedLength(pxPerFr);
				yield return isRow
					? new Rect(current, rect.y, len, rect.height)
					: new Rect(rect.x, current, rect.width, len);
				current += len;
				if (i < count - 1)
					current += effectiveGap;
			}
		}
		else {
			float current = mainAxisEnd - startOffset;
			for (var i = 0; i < count; i++) {
				float len = Lengths[i].CalculatedLength(pxPerFr);
				current -= len;
				yield return isRow
					? new Rect(current, rect.y, len, rect.height)
					: new Rect(rect.x, current, rect.width, len);
				if (i < count - 1)
					current -= effectiveGap;
			}
		}
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public readonly record struct Length(float Px, float Fr) {
		private static readonly Regex FlexPattern = new(
			@"^(?<val>\d+(?:\.\d+)?)\s*(?<unit>fr|px)?$",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
		);

		public static Length Auto { get; } = Fraction(1);

		public bool Valid => Fr > 0 || Fr == 0 && Px >= 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Length Pixel(float value) => new(value, 0f);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Length Fraction(float value) => new(0, value);

		public static bool TryParse(string s, out Length length) {
			var match = FlexPattern.Match(s);
			if (!match.Success) {
				length = 0;
				return false;
			}
			float val = float.Parse(match.Groups["val"].Value);
			string unit = match.Groups["unit"].Value.ToLowerInvariant();
			length = unit switch {
				"fr"       => Fraction(val),
				"px" or "" => Pixel(val),
				_          => throw new FormatException($"Invalid flexbox length unit: {unit}")
			};
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float CalculatedLength(float pxPerFr) => Px + Fr * pxPerFr;

		public override string ToString() {
			var frPart = Fr != 0 ? $"{Fr}fr" : null;
			var pxPart = Px != 0 ? $"{MathF.Abs(Px)}px" : null;
			return pxPart is not null && frPart is not null
				? $"{frPart}{(Px < 0 ? '-' : '+')}{pxPart}"
				: pxPart ?? frPart ?? "0px";
		}

		public static Length operator *(Length l, float k) => new(l.Px * k, l.Fr * k);

		public static Length operator /(Length l, float k) => new(l.Px / k, l.Fr / k);

		public static Length operator +(Length a, Length b) => new(a.Px + b.Px, a.Fr + b.Fr);

		public static Length operator -(Length a, Length b) => new(a.Px - b.Px, a.Fr - b.Fr);

		public static implicit operator Length(float value) => Pixel(value);

		public static implicit operator Length(string s)
			=> TryParse(s, out var length) ? length : throw new FormatException($"Invalid flexbox length format: {s}");
	}

	public IReadOnlyList<Length> Lengths { get; } = lengths switch {
		null or { Length: 0 }                    => throw new ArgumentException("At least one length must be specified.", nameof(lengths)),
		not null when lengths.Any(l => !l.Valid) => throw new ArgumentException("All lengths must be non-negative.", nameof(lengths)),
		_                                        => lengths
	};

	public FlexDirection Direction { get; init; } = FlexDirection.Row;

	public JustifyContent JustifyContent { get; init; } = JustifyContent.FlexStart;

	public Rect Rect => rect;

	public float Gap {
		get;
		init {
			if (value < 0f)
				throw new ArgumentOutOfRangeException(nameof(Gap), value, "Gap must be non-negative.");
			field = value;
		}
	} = 0f;
}

public enum JustifyContent : byte {
	FlexStart,
	FlexEnd,
	Center,
	SpaceBetween,
	SpaceAround,
	SpaceEvenly
}