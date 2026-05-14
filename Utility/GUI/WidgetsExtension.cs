using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LudeonTK;
using TrueMogician.Extensions.Enumerable;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Utility.GUI;

[Flags]
public enum BorderEdges : byte {
	Top = 1,
	Bottom = 2,
	Left = 4,
	Right = 8,
	All = Top | Bottom | Left | Right
}

public static class WidgetsExtension {
	public static void DrawBorder(Rect rect, BorderEdges edges = BorderEdges.All, int thickness = 1, Texture2D? lineTexture = null) {
		var vector = new Vector2(rect.x, rect.y);
		var vector2 = new Vector2(rect.x + rect.width, rect.y + rect.height);
		if (vector.x > vector2.x) {
			(float x1, float x2) = (vector2.x, vector.x);
			vector.x = x1;
			vector2.x = x2;
		}
		if (vector.y > vector2.y) {
			(float y1, float y2) = (vector2.y, vector.y);
			vector.y = y1;
			vector2.y = y2;
		}
		Vector3 vector3 = vector2 - vector;
		var texture = lineTexture ?? BaseContent.WhiteTex;
		if ((edges & BorderEdges.Left) != 0)
			UnityEngine.GUI.DrawTexture(UIScaling.AdjustRectToUIScaling(new Rect(vector.x, vector.y, thickness, vector3.y)), texture);
		if ((edges & BorderEdges.Right) != 0)
			UnityEngine.GUI.DrawTexture(UIScaling.AdjustRectToUIScaling(new Rect(vector2.x - thickness, vector.y, thickness, vector3.y)), texture);
		if ((edges & BorderEdges.Top) != 0) {
			float x = vector.x + ((edges & BorderEdges.Left) != 0 ? thickness : 0f);
			float width = vector3.x - ((edges & BorderEdges.Left) != 0 ? thickness : 0f) - ((edges & BorderEdges.Right) != 0 ? thickness : 0f);
			UnityEngine.GUI.DrawTexture(UIScaling.AdjustRectToUIScaling(new Rect(x, vector.y, width, thickness)), texture);
		}
		if ((edges & BorderEdges.Bottom) != 0) {
			float x = vector.x + ((edges & BorderEdges.Left) != 0 ? thickness : 0f);
			float width = vector3.x - ((edges & BorderEdges.Left) != 0 ? thickness : 0f) - ((edges & BorderEdges.Right) != 0 ? thickness : 0f);
			UnityEngine.GUI.DrawTexture(UIScaling.AdjustRectToUIScaling(new Rect(x, vector2.y - thickness, width, thickness)), texture);
		}
	}

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