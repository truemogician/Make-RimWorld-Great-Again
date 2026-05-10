using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using Steamworks;
using UnityEngine;
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

	private Vector2 _scrollPosition;

	private readonly PublishedFileId_t _publishedFileId = workshopItemId > 0 ? new PublishedFileId_t(workshopItemId) : PublishedFileId_t.Invalid;

	public StandaloneModMigrationNotice(string newPackageId, ulong newWorkshopItemId, string translationKeyPrefix) :
		this(newPackageId, newWorkshopItemId, new TranslationProvider(translationKeyPrefix)) { }

	public TaggedString Title => TranslationProvider.Translate("Title");

	public void DoContents(Rect rect) {
		const float buttonHeight = 35f;
		const float gap = 10f;
		float buttonWidth = Mathf.Min(180f, (rect.width - gap) / 2f);
		var buttonRow = new Rect(rect.x, rect.yMax - buttonHeight, rect.width, buttonHeight);
		var bodyRect = new Rect(rect.x, rect.y, rect.width, rect.height - buttonHeight - gap);
		var viewRect = new Rect(
			0f,
			0f,
			bodyRect.width - 16f,
			Text.CalcHeight(TranslationProvider.Translate("Body", WorkshopUrl), bodyRect.width - 16f)
		);
		Widgets.BeginScrollView(bodyRect, ref _scrollPosition, viewRect);
		Widgets.Label(viewRect, TranslationProvider.Translate("Body", WorkshopUrl));
		Widgets.EndScrollView();
		var subscribeButton = new Rect(buttonRow.xMax - buttonWidth, buttonRow.y, buttonWidth, buttonHeight);
		var workshopButton = new Rect(subscribeButton.x - gap - buttonWidth, buttonRow.y, buttonWidth, buttonHeight);
		if (Widgets.ButtonText(workshopButton, TranslationProvider.Translate("WorkshopPage")))
			OpenWorkshopPage();
		if (Widgets.ButtonText(subscribeButton, TranslationProvider.Translate("SubscribeAndEnable")))
			SubscribeAndEnable();
	}

	public string PackageId { get; } = packageId;

	public ulong WorkshopItemId { get; } = workshopItemId;

	public ITranslationProvider TranslationProvider { get; } = provider;

	public string WorkshopUrl => $"https://steamcommunity.com/sharedfiles/filedetails/?id={WorkshopItemId}";

	private static void OnItemInstalled(ItemInstalled_t result) {
		if (!SteamManager.Initialized || result.m_unAppID != SteamUtils.GetAppID())
			return;
		if (!PendingInstalls.TryGetValue(result.m_nPublishedFileId, out var notice))
			return;
		if (notice.TryEnableInstalledMod(true, false))
			PendingInstalls.Remove(result.m_nPublishedFileId);
		else
			notice.Notify("EnableFailed", MessageTypeDefOf.RejectInput);
	}

	private void SubscribeAndEnable() {
		if (TryEnableInstalledMod(true, true))
			return;
		if (!SteamManager.Initialized) {
			Notify("SubscribeNoSteam", MessageTypeDefOf.RejectInput);
			OpenWorkshopPage();
			return;
		}
		if (_publishedFileId == PublishedFileId_t.Invalid) {
			Notify("SubscribeUnavailable", MessageTypeDefOf.RejectInput);
			OpenWorkshopPage();
			return;
		}
		_itemInstalledCallback ??= Callback<ItemInstalled_t>.Create(OnItemInstalled);
		PendingInstalls[_publishedFileId] = this;
		if (!WorkshopItems.HasItem(_publishedFileId)) {
			try {
				_ = SteamUGC.SubscribeItem(_publishedFileId);
			}
			catch (Exception ex) {
				PendingInstalls.Remove(_publishedFileId);
				Log.Error($"Failed to subscribe to workshop item {_publishedFileId}: {ex}");
				Notify("SubscribeFailed", MessageTypeDefOf.RejectInput);
				OpenWorkshopPage();
				return;
			}
		}
		Notify("SubscribeQueued", MessageTypeDefOf.TaskCompletion);
	}

	private void OpenWorkshopPage() => SteamUtility.OpenUrl(WorkshopUrl);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void Notify(string key, MessageTypeDef type) =>
		Messages.Message(TranslationProvider.Translate(key), type, false);

	private bool TryEnableInstalledMod(bool rebuildModList, bool notifyIfAlreadyEnabled) {
		if (rebuildModList)
			ModLister.RebuildModList();
		if (ModLister.GetActiveModWithIdentifier(PackageId, true) != null) {
			if (notifyIfAlreadyEnabled)
				Notify("AlreadyEnabled", MessageTypeDefOf.TaskCompletion);
			return true;
		}
		var mod = ModLister.GetModWithIdentifier(PackageId, true);
		if (mod == null)
			return false;
		mod.Active = true;
		ModsConfig.Save();
		ModsConfig.RestartFromChangedMods();
		return true;
	}
}