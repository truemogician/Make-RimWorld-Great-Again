
using System;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Utility.GUI;

using GUI = UnityEngine.GUI;

public static class Scoped {
	public static ScopedGUI GUI(bool? enabled = null)
		=> new() { Enabled = enabled };

	public static ScopedText Text(
		TextAnchor? anchor = null,
		GameFont? font = null,
		bool? wordWrap = null
	) => new() { Anchor = anchor, Font = font, WordWrap = wordWrap };
}

public class ScopedGUI : IDisposable {
	private readonly bool? _enabled;

	public bool? Enabled {
		init {
			if (value is null)
				return;
			_enabled = GUI.enabled;
			GUI.enabled = value.Value;
		}
	}

	public void Dispose() {
		if (_enabled.HasValue)
			GUI.enabled = _enabled.Value;
	}
}

public class ScopedText : IDisposable {
	private readonly TextAnchor? _anchor;

	private readonly GameFont? _font;

	private readonly bool? _wordWrap;

	public TextAnchor? Anchor {
		init {
			if (value is null)
				return;
			_anchor = Text.Anchor;
			Text.Anchor = value.Value;
		}
	}

	public GameFont? Font {
		init {
			if (value is null)
				return;
			_font = Text.Font;
			Text.Font = value.Value;
		}
	}

	public bool? WordWrap {
		init {
			if (value is null)
				return;
			_wordWrap = Text.WordWrap;
			Text.WordWrap = value.Value;
		}
	}

	public void Dispose() {
		if (_anchor.HasValue)
			Text.Anchor = _anchor.Value;
		if (_font.HasValue)
			Text.Font = _font.Value;
		if (_wordWrap.HasValue)
			Text.WordWrap = _wordWrap.Value;
	}
}