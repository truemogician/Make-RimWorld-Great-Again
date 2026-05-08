using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TrueMogician.Extensions.Enumerable;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Utility.GUI;

public static class WidgetsExtension {
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int HorizontalSlider(
		Rect rect,
		int value,
		int min,
		int max,
		bool middleAlignment = false,
		string? label = null,
		string? leftAlignedLabel = null,
		string? rightAlignedLabel = null,
		int step = 1
	) {
		float result = Widgets.HorizontalSlider(rect, value, min, max, middleAlignment, label, leftAlignedLabel, rightAlignedLabel, step);
		return Mathf.RoundToInt(result);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int HorizontalSlider(
		Rect rect,
		int value,
		IntRange range,
		bool middleAlignment = false,
		string? label = null,
		string? leftAlignedLabel = null,
		string? rightAlignedLabel = null,
		int step = 1
	) => HorizontalSlider(rect, value, range.min, range.max, middleAlignment, label, leftAlignedLabel, rightAlignedLabel, step);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T HorizontalSlider<T>(
		Rect rect,
		int index,
		IReadOnlyList<T> choices,
		Func<T, string> labelSelector,
		bool middleAlignment = false,
		bool showLabel = true,
		bool showLeftLabel = false,
		bool showRightLabel = false
	) {
		var idx = HorizontalSlider(
			rect,
			index,
			0,
			choices.Count - 1,
			middleAlignment,
			showLabel ? labelSelector(choices[index]) : null,
			showLeftLabel ? labelSelector(choices[0]) : null,
			showRightLabel ? labelSelector(choices[^1]) : null
		);
		return choices[idx];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T HorizontalSlider<T>(
		Rect rect,
		T value,
		IReadOnlyList<T> choices,
		Func<T, string> labelSelector,
		bool middleAlignment = false,
		bool showLabel = true,
		bool showLeftLabel = false,
		bool showRightLabel = false
	) where T : IEquatable<T> {
		int index = choices.IndexOf(value);
		return index >= 0
			? HorizontalSlider(rect, index, choices, labelSelector, middleAlignment, showLabel, showLeftLabel, showRightLabel)
			: throw new ArgumentException("Value not found in choices", nameof(value));
	}
}