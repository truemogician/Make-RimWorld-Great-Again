using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.BattleDossier;

public class Settings : ModSettings {
	private const int _DEFAULT_END_CAP_TICKS = 15000;
	private const int _DEFAULT_MIN_BATTLE_SCALE = 150;

	private int _endCapTicks = _DEFAULT_END_CAP_TICKS;
	private int _minBattleScale = _DEFAULT_MIN_BATTLE_SCALE;
	private int _maxStoredDossiers;
	private bool _autoOpenWindow;

	public static Settings Default { get; internal set; } = null!;

	/// <summary>Maximum ticks a quiet session waits for threats to clear before force-ending.</summary>
	public int EndCapTicks => _endCapTicks;

	/// <summary>Minimum on-map hostile combat power for a new engagement to open its own dossier.</summary>
	public int MinBattleScale => _minBattleScale;

	/// <summary>Rolling window for stored dossiers; 0 means unlimited.</summary>
	public int MaxStoredDossiers => _maxStoredDossiers;

	/// <summary>Open the dossier window directly when a battle ends, in addition to the letter.</summary>
	public bool AutoOpenWindow => _autoOpenWindow;

	public void DoWindowContents(Rect inRect) {
		var listing = new Listing_Standard();
		listing.Begin(inRect);
		listing.CheckboxLabeled(Translate("AutoOpenWindow"), ref _autoOpenWindow, Translate("AutoOpenWindow.Tooltip"));
		listing.Label(Translate("EndCapTicks", _endCapTicks), tooltip: Translate("EndCapTicks.Tooltip"));
		_endCapTicks = (int)listing.Slider(_endCapTicks, 5000f, 60000f);
		listing.Label(Translate("MinBattleScale", _minBattleScale), tooltip: Translate("MinBattleScale.Tooltip"));
		_minBattleScale = (int)listing.Slider(_minBattleScale, 0f, 1000f);
		listing.Label(
			_maxStoredDossiers == 0 ? Translate("MaxStoredDossiers.Unlimited") : Translate("MaxStoredDossiers", _maxStoredDossiers),
			tooltip: Translate("MaxStoredDossiers.Tooltip")
		);
		_maxStoredDossiers = (int)listing.Slider(_maxStoredDossiers, 0f, 500f);
		listing.End();
	}

	public override void ExposeData() {
		Scribe_Values.Look(ref _endCapTicks, "endCapTicks", _DEFAULT_END_CAP_TICKS);
		Scribe_Values.Look(ref _minBattleScale, "minBattleScale", _DEFAULT_MIN_BATTLE_SCALE);
		Scribe_Values.Look(ref _maxStoredDossiers, "maxStoredDossiers");
		Scribe_Values.Look(ref _autoOpenWindow, "autoOpenWindow");
	}

	private static string Translate(string key, params NamedArgument[] args) => $"BattleDossier.Settings.{key}".Translate(args);
}