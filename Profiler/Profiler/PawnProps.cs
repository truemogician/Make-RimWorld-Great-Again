using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.Profiler;

public readonly struct PawnProps(Pawn pawn) {
	public bool OnActiveMap { get; } = pawn.MapHeld is not null;

	public PawnState State { get; } = GetPawnState(pawn);

	public PawnType Type { get; } = GetPawnType(pawn);

	public static PawnState GetPawnState(Pawn pawn) {
		if (pawn.Dead)
			return PawnState.Dead;
		if (pawn.InCryptosleep)
			return PawnState.Cryptosleeping;
		if (pawn.Downed)
			return PawnState.Downed;
		return PawnState.Active;
	}

	public static PawnType GetPawnType(Pawn pawn) {
		if (pawn.RaceProps is not { } race)
			return PawnType.Other;
		if (race.Humanlike) {
			if (pawn.IsFreeNonSlaveColonist)
				return PawnType.Colonist;
			if (pawn.IsPrisoner)
				return PawnType.Prisoner;
			if (pawn.IsSlave)
				return PawnType.Slave;
			return pawn.HostileTo(Faction.OfPlayer) ? PawnType.HostileHumanoid : PawnType.HarmlessHumanoid;
		}
		if (pawn.IsAnimal) {
			if (race.Dryad)
				return PawnType.Dryad;
			if (pawn.Faction == Faction.OfPlayer)
				return PawnType.TamedAnimal;
			return race.Insect ? PawnType.Insect : PawnType.WildAnimal;
		}
		if (race.IsMechanoid) {
			if (pawn.Faction == Faction.OfPlayer)
				return PawnType.WorkMech;

			if (pawn.HostileTo(Faction.OfPlayer))
				return PawnType.HostileMech;
		}
		if (race.IsDrone)
			return PawnType.Drone;
		return pawn.IsEntity ? PawnType.Entity : PawnType.Other;
	}
}

public enum PawnState : byte {
	Active,
	Downed,
	Dead,
	Cryptosleeping
}

public enum PawnType : byte {
	Colonist,
	Prisoner,
	Slave,
	HarmlessHumanoid,
	HostileHumanoid,
	Dryad,
	TamedAnimal,
	Insect,
	WildAnimal,
	WorkMech,
	HostileMech,
	Drone,
	Entity,
	Other
}