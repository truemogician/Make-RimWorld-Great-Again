using System;
using System.Reflection;
using UnityEngine;

namespace TrueMogician.RimWorld.Utility;

using static Formatter;

public class Logger(string prefix, bool raw = false) {
	public Logger(Color? color = null) : this(Colored(Assembly.GetCallingAssembly().GetName().Name, color)) { }

	public Logger(Color32? color = null) : this(Colored(Assembly.GetCallingAssembly().GetName().Name, color)) { }

	public Logger(string prefix, Color color) : this(Colored(prefix, color)) { }

	public Logger(string prefix, Color32 color) : this(Colored(prefix, color)) { }

	public string Prefix { get; } = raw ? prefix : $"[{prefix}] ";

	public bool Enabled { get; set; } =
#if DEBUG
		true;
#else
		false;
#endif

	public void Message(string text) => Log(Verse.Log.Message, text);

	public void Warning(string text, bool once = false) {
		if (once)
			LogOnce(Verse.Log.WarningOnce, text, true);
		else
			Log(Verse.Log.Warning, text, true);
	}

	public void Error(string text, bool once = false) {
		if (once)
			LogOnce(Verse.Log.ErrorOnce, text, true);
		else
			Log(Verse.Log.Error, text, true);
	}

	private void Log(Action<string> method, string text, bool important = false) {
		if (Enabled || important)
			method(Prefix + text);
	}

	private void LogOnce(Action<string, int> method, string text, bool important = false) {
		if (Enabled || important)
			method(Prefix + text, text.GetHashCode());
	}
}