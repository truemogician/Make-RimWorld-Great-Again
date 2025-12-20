using System;
using System.Reflection;

namespace TrueMogician.RimWorld.Utility;

public class Logger(string prefix, bool raw = false) {
	public Logger() : this(Assembly.GetCallingAssembly().GetName().Name) { }

	public Logger(string prefix, string color): this($"<color={color}>{prefix}</color>") { }

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