using System.Collections.Generic;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.BattleDossier.Core;

/// <summary>Signals available to a battle-start condition when a new engagement is first detected.</summary>
public readonly record struct BattleStartContext {
	public IReadOnlyList<Pawn> Concerned { get; init; }

	public Map? Map { get; init; }

	/// <summary>Hostile-to-player pawns currently spawned on the map.</summary>
	public int HostileCount { get; init; }

	/// <summary>Summed combat power of hostile-to-player pawns on the map.</summary>
	public float HostileCombatPower { get; init; }

	/// <summary>Minimum hostile combat power below which the engagement is treated as a skirmish.</summary>
	public float MinScale { get; init; }
}

public interface IBattleStartCondition {
	/// <summary>true = start a session, false = veto (skirmish), null = defer to the next condition.</summary>
	bool? Evaluate(in BattleStartContext context);
}

/// <summary>
///     Priority-ordered gate deciding whether a newly detected engagement deserves its own dossier. The
///     first condition returning a non-null verdict decides. Built-ins reject below-scale skirmishes and
///     require the player to be a party to the fight; compat modules can add their own.
/// </summary>
public static class BattleStartConditions {
	private static readonly List<(IBattleStartCondition Condition, int Priority)> _conditions = [
		(new MinScaleCondition(), 20),
		(new PlayerInvolvedCondition(), int.MinValue)
	];

	/// <summary>Registers a condition; higher <paramref name="priority" /> is consulted first.</summary>
	public static void Register(IBattleStartCondition condition, int priority = 0) {
		_conditions.Add((condition, priority));
		_conditions.SortByDescending(pair => pair.Priority);
	}

	public static bool Evaluate(in BattleStartContext context) {
		foreach (var (condition, _) in _conditions) {
			if (condition.Evaluate(in context) is { } verdict)
				return verdict;
		}
		return false;
	}
}

/// <summary>Vetoes engagements whose on-map hostile combat power is below the skirmish threshold.</summary>
public class MinScaleCondition : IBattleStartCondition {
	public bool? Evaluate(in BattleStartContext context) => context.HostileCombatPower < context.MinScale ? false : null;
}

/// <summary>
///     Requires the player to be a party to the fight: a player pawn is concerned and an opponent is
///     hostile, or (predator/manhunter cases) the map is under an active threat or elevated danger.
/// </summary>
public class PlayerInvolvedCondition : IBattleStartCondition {
	public bool? Evaluate(in BattleStartContext context) {
		var anyPlayer = false;
		var anyHostile = false;
		var anyNonPlayer = false;
		foreach (var pawn in context.Concerned) {
			if (pawn.Faction?.IsPlayer == true)
				anyPlayer = true;
			else {
				anyNonPlayer = true;
				if (pawn.HostileTo(Faction.OfPlayer))
					anyHostile = true;
			}
		}
		if (!anyPlayer)
			return false;
		if (anyHostile)
			return true;
		if (!anyNonPlayer || context.Map is not { } map)
			return false;
		return GenHostility.AnyHostileActiveThreatToPlayer(map) || map.dangerWatcher.DangerRating >= StoryDanger.Low;
	}
}