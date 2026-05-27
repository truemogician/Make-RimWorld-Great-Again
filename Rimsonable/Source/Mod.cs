using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable;

public class Mod : Verse.Mod {
	private const string _TITLE_TRANSLATION_KEY = $"{nameof(Rimsonable)}.Title";

	public Mod(ModContentPack content) : base(content) {
		Settings.Default = GetSettings<Settings>();
		Notices.Register();
		LongEventHandler.QueueLongEvent(() => Settings.Default.Apply(), $"{ThisAssembly.Info.Title}-ApplySettings", true, null);
	}

	public override string SettingsCategory() => _TITLE_TRANSLATION_KEY.TryTranslate(out var title) ? title : ThisAssembly.Info.Title;

	public override void DoSettingsWindowContents(Rect inRect) => Settings.Default.DrawContents(inRect);
}