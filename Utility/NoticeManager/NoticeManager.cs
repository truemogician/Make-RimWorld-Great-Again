using System;
using System.Collections.Generic;
using System.Linq;
using TrueMogician.RimWorld.Utility.Extensions;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Utility.NoticeManager;

public interface INotice {
	TaggedString Title { get; }

	bool ShouldShow { get; }

	void DoContents(Rect rect);
}

public static class NoticeManager {
	private const float _BUTTON_SIZE = 32f;

	private const float _BUTTON_GAP = 4f;

	private const float _OPTION_HEIGHT = 45f;

	private const float _MIN_TAB_WIDTH = 150f;

	private const float _MAX_TAB_WIDTH = 300f;

	private static List<string> _shownNoticeIds = [];

	private static readonly Dictionary<string, INotice> _notices = new();

	private static bool _pendingNoticesChanged = true;

	private static bool _noticesWindowOpen;

	public static event EventHandler? ShownNoticeIdsChanged;

	public static bool HasPendingNotices => PendingNotices.Count > 0;

	private static List<KeyValuePair<string, INotice>> PendingNotices {
		get {
			if (_pendingNoticesChanged) {
				field = _notices.Where(pair => !_shownNoticeIds.Contains(pair.Key) && pair.Value.ShouldShow).ToList();
				_pendingNoticesChanged = false;
			}
			return field;
		}
	} = [];

	public static void ExposeData() {
		Scribe_Collections.Look(ref _shownNoticeIds, "shownNoticeIds", LookMode.Value);
		_shownNoticeIds ??= [];
		_pendingNoticesChanged = true;
	}

	public static void AddNotice(string id, INotice notice) {
		if (id.NullOrEmpty())
			throw new ArgumentException("Notice ID cannot be empty.", nameof(id));
		_notices[id] = notice ?? throw new ArgumentNullException(nameof(notice));
		_pendingNoticesChanged = true;
	}

	public static void MarkShown(string id) {
		if (_shownNoticeIds.Contains(id))
			return;
		_shownNoticeIds.Add(id);
		_pendingNoticesChanged = true;
		ShownNoticeIdsChanged?.Invoke(null, EventArgs.Empty);
	}

	internal static bool TryDrawModsOptionWithNotice(ListableOption option, Vector2 pos, float width, ref float height) {
		if (Current.ProgramState != ProgramState.Entry || !HasPendingNotices || option.label != (string)"Mods".Translate())
			return false;
		float modsButtonWidth = width - _BUTTON_SIZE - _BUTTON_GAP;
		height = Mathf.Max(_OPTION_HEIGHT, Text.CalcHeight(option.label, modsButtonWidth));
		var modsButtonRect = new Rect(pos.x, pos.y, modsButtonWidth, height);
		var noticeButtonRect = new Rect(pos.x + width - _BUTTON_SIZE, pos.y + (height - _BUTTON_SIZE) / 2f, _BUTTON_SIZE, _BUTTON_SIZE);
		if (Widgets.ButtonText(modsButtonRect, option.label))
			option.action?.Invoke();
		var tooltip = PendingNotices.Select(pair => (string)pair.Value.Title).ToLineList();
		if (Widgets.ButtonImage(noticeButtonRect, TexButton.Info, Color.yellow, true, tooltip))
			ShowPendingNotices();
		return true;
	}

	private static void MarkShown(IEnumerable<string> ids) {
		int shownCount = _shownNoticeIds.Count;
		foreach (string? id in ids) {
			if (!_shownNoticeIds.Contains(id))
				_shownNoticeIds.Add(id);
		}
		if (_shownNoticeIds.Count != shownCount) {
			_pendingNoticesChanged = true;
			ShownNoticeIdsChanged?.Invoke(null, EventArgs.Empty);
		}
	}

	private static void ShowPendingNotices() {
		if (Find.WindowStack == null || _noticesWindowOpen)
			return;
		var notices = ConsumePendingNotices();
		if (notices.Count == 0)
			return;
		Find.WindowStack.Add(new NoticesWindow(notices));
		_noticesWindowOpen = true;
	}

	private static List<INotice> ConsumePendingNotices() {
		var pendingNotices = PendingNotices.ToArray();
		var notices = pendingNotices.Select(p => p.Value).ToList();
		MarkShown(pendingNotices.Select(p => p.Key));
		return notices;
	}

	private sealed class NoticesWindow : Window {
		private readonly List<INotice> _windowNotices;

		private readonly List<TabRecord> _tabs = [];

		private int _selectedIndex;

		public NoticesWindow(List<INotice> notices) {
			_windowNotices = notices;
			doCloseX = true;
			doCloseButton = false;
			closeOnAccept = false;
			closeOnCancel = true;
			forcePause = true;
			absorbInputAroundWindow = true;
			for (var i = 0; i < _windowNotices.Count; i++) {
				int index = i;
				_tabs.Add(new TabRecord(_windowNotices[index].Title, () => _selectedIndex = index, () => _selectedIndex == index));
			}
		}

		public override Vector2 InitialSize => new(Mathf.Min(UI.screenWidth - 80f, 760f), Mathf.Min(UI.screenHeight - 80f, 560f));

		public override void PostClose() {
			base.PostClose();
			_noticesWindowOpen = false;
		}

		public override void DoWindowContents(Rect inRect) {
			if (_windowNotices.Count == 0) {
				Close();
				return;
			}
			_selectedIndex = Mathf.Clamp(_selectedIndex, 0, _windowNotices.Count - 1);
			var section = inRect;
			var tabBase = section;
			section.yMin += TabDrawer.GetOverflowTabHeight(tabBase, _tabs, _MIN_TAB_WIDTH, _MAX_TAB_WIDTH);
			Widgets.DrawMenuSection(section);
			TabDrawer.DrawTabsOverflow(tabBase, _tabs, _MIN_TAB_WIDTH, _MAX_TAB_WIDTH);
			_windowNotices[_selectedIndex].DoContents(section.Padding(12f));
		}
	}
}