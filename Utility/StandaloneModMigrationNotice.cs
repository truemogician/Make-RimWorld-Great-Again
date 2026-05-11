using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Steamworks;
using TrueMogician.RimWorld.Utility.Extensions;
using TrueMogician.RimWorld.Utility.GUI;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;
using Verse.Steam;

namespace TrueMogician.RimWorld.Utility;

public sealed class StandaloneModMigrationNotice(
	string packageId,
	ulong workshopItemId,
	ITranslationProvider provider
) : INotice {
	private static readonly Dictionary<PublishedFileId_t, StandaloneModMigrationNotice> PendingInstalls = [];

	private static Callback<ItemInstalled_t>? _itemInstalledCallback;

	private static Callback<RemoteStoragePublishedFileSubscribed_t>? _itemSubscribedCallback;

	private Vector2 _scrollPosition;

	private bool _subscribing;

	private readonly PublishedFileId_t _publishedFileId = workshopItemId > 0 ? new PublishedFileId_t(workshopItemId) : PublishedFileId_t.Invalid;

	public StandaloneModMigrationNotice(string newPackageId, ulong newWorkshopItemId, string translationKeyPrefix) :
		this(newPackageId, newWorkshopItemId, new TranslationProvider(translationKeyPrefix)) { }

	public TaggedString Title => TranslationProvider.Translate("Title");

	public bool ShouldShow => !IsTargetEnabled();

	public void DoContents(Rect rect) {
		const float buttonHeight = 36f;
		const float gap = 8f;
		var rows = rect.Padding(8).ToFlexbox(FlexDirection.Column, ["1fr", buttonHeight], gap).ToArray();
		var bodyLines = ((string)TranslationProvider.Translate("Body")).Split('\n');
		var bodyWidth = rows[0].width - 16f;
		var lineHeights = bodyLines.Select(l => Text.CalcHeight(l, bodyWidth)).ToArray();
		var viewRect = new Rect(0f, 0f, bodyWidth, lineHeights.Sum() + gap * (bodyLines.Length - 1));
		Widgets.BeginScrollView(rows[0], ref _scrollPosition, viewRect);
		float curHeight = 0;
		for (var i = 0; i < bodyLines.Length; ++i) {
			Widgets.Label(new Rect(0, curHeight, viewRect.width, lineHeights[i]), bodyLines[i]);
			curHeight += lineHeights[i] + gap;
		}
		Widgets.EndScrollView();
		var buttons = rows[1].ToFlexbox(FlexDirection.Row, 2, gap).ToArray();
		if (Widgets.ButtonText(buttons[0], TranslationProvider.Translate("WorkshopPage")))
			SteamUtility.OpenWorkshopPage(_publishedFileId);
		bool enableSubscribeBtn = !_subscribing && !IsTargetEnabled();
		using (Scoped.GUI(enableSubscribeBtn)) {
			var subscribeText = TranslationProvider.Translate(_subscribing ? "Subscribing" : "SubscribeAndEnable");
			if (Widgets.ButtonText(buttons[1], subscribeText, active: enableSubscribeBtn))
				SubscribeAndEnable();
		}
	}

	public string PackageId { get; } = packageId;

	public ulong WorkshopItemId { get; } = workshopItemId;

	public ITranslationProvider TranslationProvider { get; } = provider;

	private static void OnItemSubscribed(RemoteStoragePublishedFileSubscribed_t result) {
		if (!SteamManager.Initialized || result.m_nAppID != SteamUtils.GetAppID())
			return;
		if (PendingInstalls.TryGetValue(result.m_nPublishedFileId, out var notice))
			notice.TryFinishInstallAndEnable(false);
	}

	private static void OnItemInstalled(ItemInstalled_t result) {
		if (!SteamManager.Initialized || result.m_unAppID != SteamUtils.GetAppID())
			return;
		if (PendingInstalls.TryGetValue(result.m_nPublishedFileId, out var notice))
			notice.TryFinishInstallAndEnable(true);
	}

	private void SubscribeAndEnable() {
		if (TryEnableInstalledMod(true, true))
			return;
		if (!SteamManager.Initialized) {
			Notify("SubscribeNoSteam", MessageTypeDefOf.RejectInput);
			SteamUtility.OpenWorkshopPage(_publishedFileId);
			return;
		}
		if (_publishedFileId == PublishedFileId_t.Invalid) {
			Notify("SubscribeUnavailable", MessageTypeDefOf.RejectInput);
			SteamUtility.OpenWorkshopPage(_publishedFileId);
			return;
		}
		_itemSubscribedCallback ??= Callback<RemoteStoragePublishedFileSubscribed_t>.Create(OnItemSubscribed);
		_itemInstalledCallback ??= Callback<ItemInstalled_t>.Create(OnItemInstalled);
		PendingInstalls[_publishedFileId] = this;
		if (WorkshopItems.HasItem(_publishedFileId))
			TryFinishInstallAndEnable(true);
		else {
			_subscribing = true;
			try {
				_ = SteamUGC.SubscribeItem(_publishedFileId);
			}
			catch (Exception ex) {
				PendingInstalls.Remove(_publishedFileId);
				Log.Error($"Failed to subscribe to workshop item {_publishedFileId}: {ex}");
				Notify("SubscribeFailed", MessageTypeDefOf.RejectInput);
				SteamUtility.OpenWorkshopPage(_publishedFileId);
				_subscribing = false;
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void Notify(string key, MessageTypeDef type) =>
		Messages.Message(TranslationProvider.Translate(key), type, false);

	private bool TryEnableInstalledMod(bool rebuildModList, bool notifyIfAlreadyEnabled) {
		if (IsTargetEnabled(rebuildModList)) {
			if (notifyIfAlreadyEnabled)
				Notify("AlreadyEnabled", MessageTypeDefOf.TaskCompletion);
			return true;
		}
		var mod = ModLister.GetModWithIdentifier(PackageId, true);
		if (mod == null)
			return false;
		mod.Active = true;
		ModsConfig.Save();
		_ = UtilityWindows.Confirm(
			TranslationProvider.Translate("RestartPrompt"),
			null,
			TranslationProvider.Translate("RestartNow"),
			TranslationProvider.Translate("RestartLater")
		)
		.ContinueWith(t => {
			if (t.Result)
				ModsConfig.RestartFromChangedMods();
		});
		return true;
	}

	private void TryFinishInstallAndEnable(bool notifyIfUnavailable) {
		if (TryEnableInstalledMod(true, false)) {
			PendingInstalls.Remove(_publishedFileId);
			_subscribing = false;
		}
		else if (notifyIfUnavailable) {
			Notify("EnableFailed", MessageTypeDefOf.RejectInput);
			_subscribing = false;
		}
	}

	private bool IsTargetEnabled(bool rebuildModList = false) {
		if (rebuildModList)
			ModLister.RebuildModList();
		return ModLister.GetActiveModWithIdentifier(PackageId, true) != null;
	}
}