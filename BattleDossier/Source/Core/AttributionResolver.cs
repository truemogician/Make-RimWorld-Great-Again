using System.Collections.Generic;
using Verse;

namespace TrueMogician.RimWorld.BattleDossier.Core;

public enum AttributionKind : byte {
	Damage,
	Kill,
	Down
}

/// <summary>Everything a collection hook saw about one credited event.</summary>
public readonly record struct AttributionContext {
	public Thing Victim { get; init; }

	public DamageInfo? Dinfo { get; init; }

	public DamageWorker.DamageResult? Result { get; init; }

	public AttributionKind Kind { get; init; }
}

public interface IAttributionHandler {
	/// <summary>The Thing to credit for the event, or null to defer to the next handler.</summary>
	Thing? Resolve(in AttributionContext context);
}

/// <summary>
///     Priority-ordered credit resolution for compat modules. Handlers decide <i>who</i> gets credit;
///     the collection core still decides whether it counts. The vanilla instigator logic is the lowest-priority fallback.
/// </summary>
public static class AttributionResolver {
	private static readonly List<(IAttributionHandler Handler, int Priority)> _handlers = [(new DefaultAttributionHandler(), int.MinValue)];

	/// <summary>Registers a handler; higher <paramref name="priority" /> is consulted first.</summary>
	public static void Register(IAttributionHandler handler, int priority = 0) {
		_handlers.Add((handler, priority));
		_handlers.SortByDescending(pair => pair.Priority);
	}

	public static Thing? Resolve(in AttributionContext context) {
		foreach (var (handler, _) in _handlers) {
			if (handler.Resolve(in context) is { } credited)
				return credited;
		}
		return null;
	}
}

public class DefaultAttributionHandler : IAttributionHandler {
	public Thing? Resolve(in AttributionContext context) => context.Dinfo?.Instigator;
}