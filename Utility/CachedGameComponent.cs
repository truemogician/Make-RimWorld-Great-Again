using System;
using Verse;

namespace TrueMogician.RimWorld.Utility;

public class CachedGameComponent<T> where T : GameComponent {
	private static T? _component;

	static CachedGameComponent() {
		CurrentGameHelper.GameChanged += (_, args) => { _component = args.NewGame.GetComponent<T>(); };
	}

	public static T Component {
		get {
			CurrentGameHelper.Touch();
			return _component!;
		}
	}
}

public static class CurrentGameHelper {
	private static System.WeakReference<Game>? _ref;

	public static event EventHandler<GameChangedEventArgs>? GameChanged;

	public static Game Game {
		get {
			if (_ref is null)
				return Update();
			return _ref.TryGetTarget(out var game) && ReferenceEquals(game, Current.Game) ? game : Update();
		}
	}

	public static void Touch() {
		if (_ref is null || !_ref.TryGetTarget(out var game) || !ReferenceEquals(game, Current.Game))
			Update();
	}

	private static Game Update() {
		var newGame = Current.Game;
		if (_ref is null)
			_ref = new System.WeakReference<Game>(newGame);
		else
			_ref.SetTarget(newGame);
		GameChanged?.Invoke(null, newGame);
		return newGame;
	}

	public class GameChangedEventArgs(Game newGame) : EventArgs {
		public Game NewGame { get; } = newGame;

		public static implicit operator GameChangedEventArgs(Game game) => new(game);
	}
}