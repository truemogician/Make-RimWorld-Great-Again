using System;
using TrueMogician.RimWorld.Utility;

namespace TrueMogician.RimWorld.Rimsonable;

[Flags]
internal enum NoticeShownFlags : long {
	None = 0,

	WorkMemoryMigration = 1L << 0
}

internal sealed class NoticeManager : NoticeManager<NoticeShownFlags> {
	private const ulong _WORK_MEMORY_WORKSHOP_ID = 3723997608UL;

	private const long _WORK_MEMORY_RELEASE_TIMESTAMP = 1778485275L;

	private NoticeManager() : base("Rimsonable.Migration.WindowTitle") {
		ShownFlagsChanged += (_, _) => Settings.Default.Write();
		AddNotice(
			NoticeShownFlags.WorkMemoryMigration,
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

	public static NoticeManager Instance { get; } = new();
}