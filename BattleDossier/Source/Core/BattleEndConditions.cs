using System.Collections.Generic;
using Verse;

namespace TrueMogician.RimWorld.BattleDossier.Core;

/// <summary>Signals available to a battle-end condition at evaluation time.</summary>
public readonly record struct BattleEndContext {
	public IReadOnlyList<Map> Maps { get; init; }

	/// <summary>The tick a hostile threat was last present, or -1 while a threat is still active.</summary>
	public int LastThreatTick { get; init; }

	/// <summary>The tick of the last recorded combat event in this battle.</summary>
	public int LastActivityTick { get; init; }
}

public interface IBattleEndCondition {
	/// <summary>true = battle ended, false = keep open, null = defer to the next condition.</summary>
	bool? Evaluate(in BattleEndContext context);
}

/// <summary>
///     Priority-ordered battle-end resolution for compat modules. The first condition returning a
///     non-null verdict decides. Built-ins conclude the moment threats clear and fall back to a
///     quiet-window timeout.
/// </summary>
public static class BattleEndConditions {
	private static readonly List<(IBattleEndCondition Condition, int Priority)> _conditions = [
		(new ThreatsClearedCondition(), 20),
		(new QuietTimeoutCondition(), int.MinValue)
	];

	/// <summary>Registers a condition; higher <paramref name="priority" /> is consulted first.</summary>
	public static void Register(IBattleEndCondition condition, int priority = 0) {
		_conditions.Add((condition, priority));
		_conditions.SortByDescending(pair => pair.Priority);
	}

	public static bool Evaluate(in BattleEndContext context) {
		foreach (var (condition, _) in _conditions) {
			if (condition.Evaluate(in context) is { } verdict)
				return verdict;
		}
		return false;
	}
}

/// <summary>
///     Concludes once no hostile pawn has actively threatened the player for <see cref="GRACE_TICKS" />.
///     Downed or bleeding-out enemies do not count as threats, so the battle ends promptly without waiting
///     for them to die; the grace debounces brief lulls and reinforcement gaps.
/// </summary>
public class ThreatsClearedCondition : IBattleEndCondition {
	public const int GRACE_TICKS = 2500;

	public bool? Evaluate(in BattleEndContext context) =>
		context.LastThreatTick >= 0 && Find.TickManager.TicksGame - context.LastThreatTick >= GRACE_TICKS ? true : null;
}

/// <summary>Fallback: force-ends after the configured quiet cap even if a threat lingers (e.g. unreachable).</summary>
public class QuietTimeoutCondition : IBattleEndCondition {
	public bool? Evaluate(in BattleEndContext context) =>
		Find.TickManager.TicksGame - context.LastActivityTick >= Settings.Default.EndCapTicks;
}