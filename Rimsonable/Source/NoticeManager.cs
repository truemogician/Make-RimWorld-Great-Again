using System;
using TrueMogician.RimWorld.Utility;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable;

[Flags]
internal enum NoticeShownFlags : long {
	None = 0,

	WorkMemoryMigration = 1L << 0
}

internal sealed class NoticeManager : NoticeManager<NoticeShownFlags> {
	private NoticeManager() : base(() => "Rimsonable.Title".Translate()) {
		ShownFlagsChanged += (_, _) => Settings.Default.Write();
		AddNotice(
			NoticeShownFlags.WorkMemoryMigration,
			new StandaloneModMigrationNotice(
				"Rimsonable.WorkMemoryMigration",
				"TrueMogician.WorkMemory",
				0 // TODO replace with the published Workshop id.
			)
		);
	}

	public static NoticeManager Instance { get; } = new();
}