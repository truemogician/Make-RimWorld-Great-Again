using System;

namespace TrueMogician.RimWorld.Utility.GUI;

using GUI = UnityEngine.GUI;

public static class Scoped {
	public static ScopedGUI GUI(bool? enabled = null)
		=> new() { Enabled = enabled };
}

public class ScopedGUI : IDisposable {
	private readonly bool? _enabled;

	public void Dispose() {
		if (_enabled.HasValue)
			GUI.enabled = _enabled.Value;
	}

	public bool? Enabled {
		init {
			if (value is null)
				return;
			_enabled = GUI.enabled;
			GUI.enabled = value.Value;
		}
	}
}