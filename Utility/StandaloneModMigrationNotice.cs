using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Steamworks;
using UnityEngine;
using Verse;
using Verse.Steam;

namespace TrueMogician.RimWorld.Utility;

public sealed class StandaloneModMigrationNotice(
	string translationKeyPrefix,
	string targetPackageId,
	ulong workshopItemId
) : INotice {
	private static readonly Dictionary<PublishedFileId_t, StandaloneModMigrationNotice> PendingInstalls = [];

	private static Callback<ItemInstalled_t>? _itemInstalledCallback;

	private Vector2 _scrollPosition;

	public TaggedString Title => Translate("Title");

	public void DoContents(Rect rect) {
		const float buttonHeight = 35f;
		const float gap = 10f;
		float buttonWidth = Mathf.Min(180f, (rect.width - gap) / 2f);
		var buttonRow = new Rect(rect.x, rect.yMax - buttonHeight, rect.width, buttonHeight);
		var bodyRect = new Rect(rect.x, rect.y, rect.width, rect.height - buttonHeight - gap);
		var viewRect = new Rect(0f, 0f, bodyRect.width - 16f, Text.CalcHeight(Translate("Body", WorkshopUrl), bodyRect.width - 16f));
		Widgets.BeginScrollView(bodyRect, ref _scrollPosition, viewRect);
		Widgets.Label(viewRect, Translate("Body", WorkshopUrl));
		Widgets.EndScrollView();
		var subscribeButton = new Rect(buttonRow.xMax - buttonWidth, buttonRow.y, buttonWidth, buttonHeight);
		var workshopButton = new Rect(subscribeButton.x - gap - buttonWidth, buttonRow.y, buttonWidth, buttonHeight);
		if (Widgets.ButtonText(workshopButton, Translate("WorkshopPage")))
			OpenWorkshopPage();
		if (Widgets.ButtonText(subscribeButton, Translate("SubscribeAndEnable")))
			SubscribeAndEnable();
	}

	public string TranslationKeyPrefix { get; } = translationKeyPrefix;

	public string TargetPackageId { get; } = targetPackageId;

	public ulong WorkshopItemId { get; } = workshopItemId;

	public string WorkshopUrl => $"https://steamcommunity.com/sharedfiles/filedetails/?id={WorkshopItemId}";

	private PublishedFileId_t PublishedFileId => WorkshopItemId > 0 ? new PublishedFileId_t(WorkshopItemId) : PublishedFileId_t.Invalid;

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
		if (PublishedFileId == PublishedFileId_t.Invalid) {
			Notify("SubscribeUnavailable", MessageTypeDefOf.RejectInput);
			OpenWorkshopPage();
			return;
		}
		_itemInstalledCallback ??= Callback<ItemInstalled_t>.Create(OnItemInstalled);
		PendingInstalls[PublishedFileId] = this;
		if (!WorkshopItems.HasItem(PublishedFileId)) {
			try {
				_ = SteamUGC.SubscribeItem(PublishedFileId);
			}
			catch (Exception ex) {
				PendingInstalls.Remove(PublishedFileId);
				Log.Error($"Failed to subscribe to workshop item {PublishedFileId}: {ex}");
				Notify("SubscribeFailed", MessageTypeDefOf.RejectInput);
				OpenWorkshopPage();
				return;
			}
		}
		Notify("SubscribeQueued", MessageTypeDefOf.TaskCompletion);
	}

	private void OpenWorkshopPage() => SteamUtility.OpenUrl(WorkshopUrl);

	private void Notify(string suffix, MessageTypeDef type)
		=> Messages.Message(Translate(suffix), type, false);

	private TaggedString Translate(string suffix, params object[] args)
		=> args.Length == 0
			? $"{TranslationKeyPrefix}.{suffix}".Translate()
			: $"{TranslationKeyPrefix}.{suffix}".Translate(args.Select(arg => new NamedArgument(arg, null)).ToArray());

	private bool TryEnableInstalledMod(bool rebuildModList, bool notifyIfAlreadyEnabled) {
		if (rebuildModList)
			ModLister.RebuildModList();
		if (ModLister.GetActiveModWithIdentifier(TargetPackageId, true) != null) {
			if (notifyIfAlreadyEnabled)
				Notify("AlreadyEnabled", MessageTypeDefOf.TaskCompletion);
			return true;
		}
		var mod = ModLister.GetModWithIdentifier(TargetPackageId, true);
		if (mod == null)
			return false;
		mod.Active = true;
		ModsConfig.Save();
		ModsConfig.RestartFromChangedMods();
		return true;
	}
}