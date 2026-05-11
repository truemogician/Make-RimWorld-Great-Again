using System;
using System.Collections.Generic;
using System.Linq;
using TrueMogician.RimWorld.Utility.Extensions;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Utility;

public interface INotice {
	TaggedString Title { get; }

	bool ShouldShow { get; }

	void DoContents(Rect rect);
}

public abstract class NoticeManager<T>(string titleTranslationKey) : IExposable where T : struct, Enum {
	private const float _MIN_TAB_WIDTH = 150f;

	private const float _MAX_TAB_WIDTH = 300f;

	private long _shownFlags;

	private readonly Dictionary<T, INotice> _notices = new();

	protected event EventHandler? ShownFlagsChanged;

	public virtual void ExposeData() {
		Scribe_Values.Look(ref _shownFlags, "noticeShownFlags");
	}

	public void AddNotice(T flag, INotice notice) {
		if (Convert.ToInt64(flag) == 0)
			throw new ArgumentException("Notice flag cannot be zero.", nameof(flag));
		_notices[flag] = notice ?? throw new ArgumentNullException(nameof(notice));
	}

	public void RegisterShowingNoticesWhenLoaded() {
		if (_notices.All(p => IsShown(p.Key)))
			return;
		if (!LongEventHandler.AnyEventNowOrWaiting && Find.WindowStack != null) {
			ShowUnshownNotices();
			return;
		}
		LongEventHandler.QueueLongEvent(
			() => { },
			null,
			false,
			null,
			false,
			callback: ShowUnshownNotices
		);
	}

	private bool IsShown(T flag) => (_shownFlags & Convert.ToInt64(flag)) != 0;

	private void MarkShown(IEnumerable<T> flags) {
		long shownFlags = _shownFlags;
		foreach (var flag in flags)
			_shownFlags |= Convert.ToInt64(flag);
		if (_shownFlags != shownFlags)
			ShownFlagsChanged?.Invoke(this, EventArgs.Empty);
	}

	private void ShowUnshownNotices() {
		if (Find.WindowStack == null)
			return;
		var unshownNotices = _notices.Where(pair => !IsShown(pair.Key)).ToArray();
		var notices = unshownNotices.Select(p => p.Value).Where(n => n.ShouldShow).ToList();
		MarkShown(unshownNotices.Select(p => p.Key));
		if (notices.Count == 0)
			return;
		Find.WindowStack.Add(new NoticesWindow(titleTranslationKey, notices));
	}

	private sealed class NoticesWindow : Window {
		private readonly string _titleTranslationKey;

		private readonly List<INotice> _notices;

		private readonly List<TabRecord> _tabs = [];

		private int _selectedIndex;

		public NoticesWindow(string titleTranslationKey, List<INotice> notices) {
			_titleTranslationKey = titleTranslationKey;
			_notices = notices;
			doCloseX = true;
			doCloseButton = false;
			closeOnAccept = false;
			closeOnCancel = true;
			forcePause = true;
			absorbInputAroundWindow = true;
			for (var i = 0; i < _notices.Count; i++) {
				int index = i;
				_tabs.Add(new TabRecord(_notices[index].Title, () => _selectedIndex = index, () => _selectedIndex == index));
			}
		}

		public override Vector2 InitialSize => new(Mathf.Min(UI.screenWidth - 80f, 760f), Mathf.Min(UI.screenHeight - 80f, 560f));

		public override void DoWindowContents(Rect inRect) {
			if (_notices.Count == 0) {
				Close();
				return;
			}
			_selectedIndex = Mathf.Clamp(_selectedIndex, 0, _notices.Count - 1);
			using (new TextBlock(GameFont.Medium))
				Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), _titleTranslationKey.Translate());
			var section = new Rect(0f, 44f, inRect.width, inRect.height - 44f);
			var tabBase = section;
			section.yMin += TabDrawer.GetOverflowTabHeight(tabBase, _tabs, _MIN_TAB_WIDTH, _MAX_TAB_WIDTH);
			Widgets.DrawMenuSection(section);
			TabDrawer.DrawTabsOverflow(tabBase, _tabs, _MIN_TAB_WIDTH, _MAX_TAB_WIDTH);
			_notices[_selectedIndex].DoContents(section.Padding(12f));
		}
	}
}