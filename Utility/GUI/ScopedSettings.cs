
using System;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Utility.GUI;

using GUI = UnityEngine.GUI;

public static class Scoped {
	public static ScopedGUI GUI(bool? enabled = null)
		=> new() { Enabled = enabled };

	public static ScopedText Text(TextAnchor? anchor = null, GameFont? font = null)
		=> new() { Anchor = anchor, Font = font };
}

public class ScopedGUI : IDisposable {
	private readonly bool? _oldEnabled;

	public bool? Enabled {
		init {
			if (value is null)
				return;
			_oldEnabled = GUI.enabled;
			GUI.enabled = value.Value;
		}
	}

	public void Dispose() {
		if (_oldEnabled.HasValue)
			GUI.enabled = _oldEnabled.Value;
	}
}

public class ScopedText : IDisposable {
	private readonly TextAnchor? _oldAnchor;

	private readonly GameFont? _oldFont;

	public TextAnchor? Anchor {
		init {
			if (value is null)
				return;
			_oldAnchor = Text.Anchor;
			Text.Anchor = value.Value;
		}
	}

	public GameFont? Font {
		init {
			if (value is null)
				return;
			_oldFont = Text.Font;
			Text.Font = value.Value;
		}
	}

	public void Dispose() {
		if (_oldAnchor.HasValue)
			Text.Anchor = _oldAnchor.Value;
		if (_oldFont.HasValue)
			Text.Font = _oldFont.Value;
	}
}