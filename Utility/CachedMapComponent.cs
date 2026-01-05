using System.Runtime.CompilerServices;
using Verse;

namespace TrueMogician.RimWorld.Utility;

public static class CachedMapComponent<T> where T : MapComponent {
	private static readonly ConditionalWeakTable<Map, T> _components = new();

	public static T? Get(Map map) {
		if (!_components.TryGetValue(map, out var component)) {
			component = map.GetComponent<T>();
			_components.Add(map, component);
		}
		return component;
	}
}