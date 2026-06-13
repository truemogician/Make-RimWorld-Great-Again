using System;
using System.Collections.Generic;
using System.Linq;
using TrueMogician.RimWorld.FlippedBuildings.Core;
using TrueMogician.RimWorld.Utility.Extensions;
using TrueMogician.RimWorld.Utility.GUI;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.FlippedBuildings;

// Master toggle gates twin generation (load-time). Per-building toggles are a live UI gate (no restart),
// persisted by defName; everything is enabled by default.
public class Settings : ModSettings {
	private const float _ROW_HEIGHT = 32f;

	private const float _ICON_SIZE = 28f;

	private const float _HEADER_HEIGHT = 78f;

	private bool _masterEnabled = true;

	private HashSet<string> _disabledDefNames = [];

	private string _searchText = "";

	private Vector2 _scrollPosition;

	public bool MasterEnabled => _masterEnabled;

	public bool IsFlipAllowed(ThingDef canonical) => _masterEnabled && !_disabledDefNames.Contains(canonical.defName);

	public override void ExposeData() {
		base.ExposeData();
		Scribe_Values.Look(ref _masterEnabled, "masterEnabled", true);
		Scribe_Collections.Look(ref _disabledDefNames, "disabledDefNames", LookMode.Value);
		_disabledDefNames ??= [];
	}

	public void DoWindowContents(Rect inRect) {
		var headerRect = inRect.Padding(0f, 0f, inRect.height - _HEADER_HEIGHT, 0f);
		var listing = new Listing_Standard();
		listing.Begin(headerRect);
		listing.CheckboxLabeled("FlippedBuildings.Settings.Master".Translate(), ref _masterEnabled, "FlippedBuildings.Settings.MasterDesc".Translate());
		var prevColor = GUI.color;
		var prevFont = Text.Font;
		GUI.color = Color.yellow;
		Text.Font = GameFont.Tiny;
		listing.Label("FlippedBuildings.Settings.RestartNote".Translate());
		Text.Font = prevFont;
		GUI.color = prevColor;
		listing.End();

		var listRect = inRect;
		listRect.yMin += _HEADER_HEIGHT;
		DrawCandidateList(listRect);
	}

	private void DrawCandidateList(Rect rect) {
		var candidates = FlipRegistry.Candidates;
		if (candidates.Count == 0) {
			using (new TextBlock(TextAnchor.UpperCenter))
				Widgets.Label(rect, "FlippedBuildings.Settings.NoneDetected".Translate());
			return;
		}

		var header = rect.Padding(0f, 0f, rect.height - _ROW_HEIGHT, 0f);
		var headerCols = header.ToFlexbox([200f, Flexbox.Length.Auto, 120f, 120f], 8f).ToArray();
		_searchText = Widgets.TextField(headerCols[0], _searchText);
		using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleLeft))
			Widgets.Label(headerCols[1], "FlippedBuildings.Settings.Count".Translate(candidates.Count));
		if (Widgets.ButtonText(headerCols[2], "FlippedBuildings.Settings.EnableAll".Translate()))
			_disabledDefNames.Clear();
		if (Widgets.ButtonText(headerCols[3], "FlippedBuildings.Settings.DisableAll".Translate()))
			_disabledDefNames = [..candidates.Select(c => c.DefName)];

		var visible = candidates
			.Where(c => _searchText.NullOrEmpty() || c.Label.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0)
			.ToList();

		var bodyRect = rect;
		bodyRect.yMin = header.yMax + 4f;
		var viewRect = new Rect(0f, 0f, bodyRect.width - 16f, visible.Count * _ROW_HEIGHT);
		Widgets.BeginScrollView(bodyRect, ref _scrollPosition, viewRect);
		var y = 0f;
		foreach (var candidate in visible) {
			DrawCandidateRow(new Rect(0f, y, viewRect.width, _ROW_HEIGHT), candidate);
			y += _ROW_HEIGHT;
		}
		Widgets.EndScrollView();
	}

	private void DrawCandidateRow(Rect rect, FlipCandidate candidate) {
		if (Mouse.IsOver(rect))
			Widgets.DrawHighlight(rect);
		var iconRect = new Rect(rect.x + 4f, rect.y + (rect.height - _ICON_SIZE) / 2f, _ICON_SIZE, _ICON_SIZE);
		if (candidate.Canonical.uiIcon != null)
			Widgets.DefIcon(iconRect, candidate.Canonical);

		var enabled = !_disabledDefNames.Contains(candidate.DefName);
		var before = enabled;
		var labelRect = new Rect(iconRect.xMax + 8f, rect.y, rect.width - iconRect.width - 48f, rect.height);
		Widgets.CheckboxLabeled(labelRect, $"{candidate.Label.CapitalizeFirst()}  <color=#9a9a9a>({candidate.ModName})</color>", ref enabled);
		if (enabled == before)
			return;
		if (enabled)
			_disabledDefNames.Remove(candidate.DefName);
		else
			_disabledDefNames.Add(candidate.DefName);
	}
}