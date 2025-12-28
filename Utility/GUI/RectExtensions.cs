using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TrueMogician.RimWorld.Utility.GUI;

public static class RectExtensions {
	private static readonly Regex FlexPattern = new(
		@"^(?<val>\d+(?:\.\d+)?)\s*(?<unit>fr|px)?$",
		RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
	);

	extension(Rect rect) {
		public List<Rect> FlexBox(IEnumerable<string> lengths) {
			var lens = lengths.ToArray();
			var results = new List<Rect>(lens.Length);
			var parsedItems = new (float val, bool fr)[lens.Length];

			var totalFixedPx = 0f;
			var totalFr = 0f;
			for (var i = 0; i < lens.Length; i++) {
				var match = FlexPattern.Match(lens[i]);
				if (!match.Success)
					throw new FormatException($"Invalid FlexBox length format: {lens[i]}");
				float val = float.Parse(match.Groups["val"].Value);
				string unit = match.Groups["unit"].Value.ToLowerInvariant();
				parsedItems[i] = (val, unit == "fr");
				if (unit == "fr")
					totalFr += val;
				else
					totalFixedPx += val;
			}

			if (totalFixedPx > rect.width) {
				throw new ArgumentOutOfRangeException(
					nameof(lens),
					$"Total fixed width ({totalFixedPx}px) exceeds the available Rect width ({rect.width}px)."
				);
			}

			float availableSpace = rect.width - totalFixedPx;
			float pxPerFr = totalFr > 0 ? availableSpace / totalFr : 0;
			float currentX = rect.x;
			foreach ((float val, bool fr) in parsedItems) {
				float width = fr ? val * pxPerFr : val;
				results.Add(new Rect(currentX, rect.y, width, rect.height));
				currentX += width;
			}

			return results;
		}
	}
}