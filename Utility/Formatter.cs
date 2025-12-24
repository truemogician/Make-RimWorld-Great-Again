using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TrueMogician.RimWorld.Utility;

public static class Formatter {
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Colored(string text, string color) => $"<color={color}>{text}</color>";

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Colored(IFormattable obj, string color) => Colored(obj.ToString(), color);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Colored(string text, Color color) => Colored(text, $"#{ColorUtility.ToHtmlStringRGB(color)}");

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Colored(IFormattable obj, Color color) => Colored(obj.ToString(), color);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Colored(string text, Color? color) => color is null ? text : Colored(text, color.Value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Colored(IFormattable obj, Color? color) => Colored(obj.ToString(), color);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Bold(string text) => $"<b>{text}</b>";

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Bold(IFormattable obj) => Bold(obj.ToString());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Italic(string text) => $"<i>{text}</i>";

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Italic(IFormattable obj) => Italic(obj.ToString());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Size(string text, int size) => $"<size={size}>{text}</size>";

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Size(IFormattable obj, int size) => Size(obj.ToString(), size);

	public static string Styled(string text, string? color = null, bool bold = false, bool italic = false, int? size = null) {
		if (bold)
			text = Bold(text);
		if (italic)
			text = Italic(text);
		if (size.HasValue)
			text = Size(text, size.Value);
		if (color is not null)
			text = Colored(text, color);
		return text;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Styled(IFormattable obj, string? color = null, bool bold = false, bool italic = false, int? size = null) =>
		Styled(obj.ToString(), color, bold, italic, size);
}

public class StyledString(string text) {
	public StyledString(IFormattable obj) : this(obj.ToString()) { }

	public string Text { get; } = text;

	public string? Color { get; set; }

	public bool Bold { get; set; }

	public bool Italic { get; set; }

	public int? Size { get; set; }

	public override string ToString() => Formatter.Styled(Text, Color, Bold, Italic, Size);

	public static explicit operator StyledString(string text) => new(text);

	public static implicit operator string(StyledString styledString) => styledString.ToString();
}

public class StyleBuilder(string text) {
	private readonly StyledString _styledString = new(text);

	public StyleBuilder(IFormattable obj) : this(obj.ToString()) { }

	public StyleBuilder Color(string color) {
		_styledString.Color = color;
		return this;
	}

	public StyleBuilder Color(Color color) {
		_styledString.Color = $"#{ColorUtility.ToHtmlStringRGB(color)}";
		return this;
	}

	public StyleBuilder Bold(bool bold = true) {
		_styledString.Bold = bold;
		return this;
	}

	public StyleBuilder Italic(bool italic = true) {
		_styledString.Italic = italic;
		return this;
	}

	public StyleBuilder Size(int size) {
		_styledString.Size = size;
		return this;
	}

	public override string ToString() => _styledString.ToString();

	public static implicit operator string(StyleBuilder styleBuilder) => styleBuilder.ToString();
}