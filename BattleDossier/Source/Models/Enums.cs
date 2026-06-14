namespace TrueMogician.RimWorld.BattleDossier.Models;

public enum BattleSide : byte {
	Colony,
	Ally,
	Enemy,
	Wild
}

public enum ParticipantFate : byte {
	Intact, // Pawn -> Unwounded; Building -> Intact
	Hit,    // Pawn -> Wounded; Building -> Damaged
	Downed,
	Dead, // Pawn -> Killed; Building -> Destroyed
	Fled,
	Captured
}

public enum ParticipantType : byte {
	Humanlike,
	Animal,
	Mechanoid,
	Building,
	Other
}

public enum BattleOutcome : byte {
	InProgress,
	Victory,
	Defeat,
	Expired
}

public enum CasualtyType : byte {
	Downed,
	Killed,
	Destroyed,
	Fled,
	Captured
}

public enum MarkerType : byte {
	BattleStarted,
	FrontMerged,
	BattleEnded
}