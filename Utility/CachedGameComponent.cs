using System;
using Verse;

namespace TrueMogician.RimWorld.Utility;

public static class CachedGameComponent<T> where T : GameComponent {
	private static Game? _game;
	private static T? _component;

	public static T Component => Get(Current.Game);

	public static T Get(Game? game) {
		if (game is null)
			throw new InvalidOperationException("No active game.");
		if (!ReferenceEquals(_game, game) || _component is null) {
			_game = game;
			_component = game.GetComponent<T>();
		}
		return _component ?? throw new InvalidOperationException(
			$"Game component {typeof(T).FullName} is unavailable."
		);
	}

	public static T? TryGet(Game? game = null) {
		game ??= Current.Game;
		if (game is null)
			return null;
		if (!ReferenceEquals(_game, game) || _component is null) {
			_game = game;
			_component = game.GetComponent<T>();
		}
		return _component;
	}

	public static void Reset() {
		_game = null;
		_component = null;
	}
}