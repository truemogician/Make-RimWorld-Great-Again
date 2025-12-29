using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
		float totalFixedPx = Lengths.Where(l => l.UnitType == Length.Unit.Px).Sum(l => l.Value);
		float totalFr = Lengths.Where(l => l.UnitType == Length.Unit.Fr).Sum(l => l.Value);
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
				(float val, var unit) = Lengths[i];
				float len = unit == Length.Unit.Fr ? val * pxPerFr : val;
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
				(float val, var unit) = Lengths[i];
				float len = unit == Length.Unit.Fr ? val * pxPerFr : val;
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

	public record Length(float Value, Length.Unit UnitType) {
		private static readonly Regex FlexPattern = new(
			@"^(?<val>\d+(?:\.\d+)?)\s*(?<unit>fr|px)?$",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
		);

		public enum Unit : byte {
			Px,
			Fr
		}

		public static Length Px(float value) => new(value, Unit.Px);

		public static Length Fr(float value) => new(value, Unit.Fr);

		public static bool TryParse(string s, [MaybeNullWhen(false)] out Length length) {
			var match = FlexPattern.Match(s);
			if (!match.Success) {
				length = null;
				return false;
			}
			float val = float.Parse(match.Groups["val"].Value);
			string unit = match.Groups["unit"].Value.ToLowerInvariant();
			length = new Length(
				val,
				unit switch {
					"fr"       => Unit.Fr,
					"px" or "" => Unit.Px,
					_          => throw new FormatException($"Invalid unit in flexbox length: {unit}")
				}
			);
			return true;
		}

		public override string ToString() => UnitType switch {
			Unit.Px => $"{Value:F1}px",
			Unit.Fr => $"{Value:F1}fr",
			_       => throw new InvalidOperationException()
		};

		public static implicit operator Length(float value) => Px(value);

		public static implicit operator Length(string s)
			=> TryParse(s, out var length) ? length : throw new FormatException($"Invalid flexbox length format: {s}");
	}

	public IReadOnlyList<Length> Lengths { get; } =
		lengths.Length > 0 ? lengths : throw new ArgumentException("At least one length must be specified.", nameof(lengths));

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