using System;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.NoticeManager;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable;

internal static class Notices {
	private const string _WORK_MEMORY_NOTICE_ID = "Rimsonable.WorkMemoryMigration";

	private const ulong _WORK_MEMORY_WORKSHOP_ID = 3723997608UL;

	private const long _WORK_MEMORY_RELEASE_TIMESTAMP = 1778485275L;

	private const long _LEGACY_WORK_MEMORY_SHOWN_FLAG = 1L << 0;

	public static void Register() {
		NoticeManager.ShownNoticeIdsChanged += (_, _) => Settings.Default.Write();
		NoticeManager.AddNotice(
			_WORK_MEMORY_NOTICE_ID,
			new StandaloneModMigrationNotice(
				"TrueMogician.WorkMemory",
				_WORK_MEMORY_WORKSHOP_ID,
				new TranslationProvider("Rimsonable.Migration.WorkMemory") {
					KeyTransformer = key => key != "Body" ? null : $"Body.{(Settings.Default[Features.WorkMemory] ? "Enabled" : "Disabled")}"
				},
				DateTimeOffset.FromUnixTimeSeconds(_WORK_MEMORY_RELEASE_TIMESTAMP)
			)
		);
	}

	public static void ExposeData() {
		NoticeManager.ExposeData();
		if (Scribe.mode == LoadSaveMode.LoadingVars) {
			var legacyShownFlags = 0L;
			Scribe_Values.Look(ref legacyShownFlags, "noticeShownFlags");
			if ((legacyShownFlags & _LEGACY_WORK_MEMORY_SHOWN_FLAG) != 0)
				NoticeManager.MarkShown(_WORK_MEMORY_NOTICE_ID);
		}
	}
}