using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Steamworks;
using TrueMogician.RimWorld.Utility.Extensions;
using TrueMogician.RimWorld.Utility.GUI;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;
using Verse.Steam;

namespace TrueMogician.RimWorld.Utility;

public sealed class StandaloneModMigrationNotice : INotice {
	private static readonly Dictionary<PublishedFileId_t, StandaloneModMigrationNotice> PendingInstalls = [];

	private static Callback<ItemInstalled_t>? _itemInstalledCallback;

	private static Callback<RemoteStoragePublishedFileSubscribed_t>? _itemSubscribedCallback;

	private static readonly Func<string, string, string> GetSettingsFilename = 
		AccessTools.MethodDelegate<Func<string, string, string>>(
			AccessTools.Method(typeof(LoadedModManager), "GetSettingsFilename")
		);

	private Vector2 _scrollPosition;

	private bool _subscribing;

	private readonly PublishedFileId_t _publishedFileId;

	private readonly Assembly _sourceAssembly;

	[MethodImpl(MethodImplOptions.NoInlining)]
	public StandaloneModMigrationNotice(
		string packageId,
		ulong workshopItemId,
		ITranslationProvider provider,
		DateTimeOffset? releaseTimestamp = null
	) : this(packageId, workshopItemId, provider, releaseTimestamp, Assembly.GetCallingAssembly()) { }

	[MethodImpl(MethodImplOptions.NoInlining)]
	public StandaloneModMigrationNotice(
		string packageId,
		ulong workshopItemId,
		string translationKeyPrefix,
		DateTimeOffset? releaseTimestamp = null
	) : this(packageId, workshopItemId, new TranslationProvider(translationKeyPrefix), releaseTimestamp, Assembly.GetCallingAssembly()) { }

	private StandaloneModMigrationNotice(
		string packageId,
		ulong workshopItemId,
		ITranslationProvider provider,
		DateTimeOffset? releaseTimestamp,
		Assembly sourceAssembly
	) {
		PackageId = packageId;
		WorkshopItemId = workshopItemId;
		TranslationProvider = provider;
		ReleaseTimestamp = releaseTimestamp;
		_sourceAssembly = sourceAssembly;
		_publishedFileId = workshopItemId > 0 ? new PublishedFileId_t(workshopItemId) : PublishedFileId_t.Invalid;
	}

	public TaggedString Title => TranslationProvider.Translate("Title");

	public bool ShouldShow => !IsTargetEnabled() && HasExistingSourceInstall();

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

	public string PackageId { get; }

	public ulong WorkshopItemId { get; }

	public ITranslationProvider TranslationProvider { get; }

	public DateTimeOffset? ReleaseTimestamp { get; }

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

	private static bool SourceSettingsFileExists(Mod mod) => 
		mod.Content != null && File.Exists(GetSettingsFilename(mod.Content.FolderName, mod.GetType().Name));

	private static bool SourceInstallPredatesRelease(Mod? mod, DateTimeOffset releaseTimestamp) {
		var sourceFileId = mod?.Content?.ModMetaData?.GetPublishedFileId() ?? PublishedFileId_t.Invalid;
		return sourceFileId != PublishedFileId_t.Invalid
			&& TryGetInstallTimestamp(sourceFileId, out var installTimestamp)
			&& installTimestamp < releaseTimestamp;
	}

	private static bool TryGetInstallTimestamp(PublishedFileId_t fileId, out DateTimeOffset installTimestamp) {
		installTimestamp = default;
		if (!SteamManager.Initialized)
			return false;
		if (!SteamUGC.GetItemInstallInfo(fileId, out _, out _, 1024, out var timestamp) || timestamp == 0)
			return false;
		installTimestamp = DateTimeOffset.FromUnixTimeSeconds(timestamp);
		return true;
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

	private bool HasExistingSourceInstall() {
		var mod = GetSourceModHandle();
		if (mod != null && SourceSettingsFileExists(mod))
			return true;
		return ReleaseTimestamp is { } releaseTimestamp && SourceInstallPredatesRelease(mod, releaseTimestamp);
	}

	private Mod? GetSourceModHandle() =>
		LoadedModManager.ModHandles.FirstOrDefault(m => m.GetType().Assembly == _sourceAssembly)
		?? LoadedModManager.ModHandles.FirstOrDefault(m => m.Content?.assemblies.loadedAssemblies.Contains(_sourceAssembly) == true);
}