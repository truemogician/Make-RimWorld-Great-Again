using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TrueMogician.RimWorld.Utility.GUI;

public class Flexbox(Rect rect, params Flexbox.Length[] lengths) : IEnumerable<Rect> {
	public IEnumerator<Rect> GetEnumerator() {
		float totalFixedPx = Lengths.Where(l => l.UnitType == Length.Unit.Px).Sum(l => l.Value);
		float totalFr = Lengths.Where(l => l.UnitType == Length.Unit.Fr).Sum(l => l.Value);
		float totalGapPx = Gap * (Lengths.Count - 1);
		float totalFixedAndGapPx = totalFixedPx + totalGapPx;

		if (totalFixedAndGapPx > rect.width) {
			throw new ArgumentOutOfRangeException(
				nameof(Lengths),
				$"Total fixed width + gaps ({totalFixedAndGapPx}px) exceeds the available Rect width ({rect.width}px)."
			);
		}

		float availableSpace = rect.width - totalFixedAndGapPx;
		float pxPerFr = totalFr > 0 ? availableSpace / totalFr : 0;
		float currentX = rect.x;
		for (var i = 0; i < Lengths.Count; i++) {
			(float val, var unit) = Lengths[i];
			float width = unit == Length.Unit.Fr ? val * pxPerFr : val;
			yield return new Rect(currentX, rect.y, width, rect.height);
			currentX += width;
			if (i < Lengths.Count - 1)
				currentX += Gap;
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

	public IReadOnlyList<Length> Lengths { get; } = lengths;

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