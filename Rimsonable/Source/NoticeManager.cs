using System;
using TrueMogician.RimWorld.Utility;

namespace TrueMogician.RimWorld.Rimsonable;

[Flags]
internal enum NoticeShownFlags : long {
	None = 0,

	WorkMemoryMigration = 1L << 0
}

internal sealed class NoticeManager : NoticeManager<NoticeShownFlags> {
	private NoticeManager() : base("Rimsonable.Migration.WindowTitle") {
		ShownFlagsChanged += (_, _) => Settings.Default.Write();
		AddNotice(
			NoticeShownFlags.WorkMemoryMigration,
			new StandaloneModMigrationNotice(
				"TrueMogician.WorkMemory",
				3723997608,
				new TranslationProvider("Rimsonable.Migration.WorkMemory") {
					KeyTransformer = key => key != "Body" ? null : $"Body.{(Settings.Default[Features.WorkMemory] ? "Enabled" : "Disabled")}"
				}
			)
		);
	}

	public static NoticeManager Instance { get; } = new();
}