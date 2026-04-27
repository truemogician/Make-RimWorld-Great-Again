using System;
using System.Collections.Generic;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage;

public static class DefCache {
	private static readonly Dictionary<ThingDef, int> _stackLimits = new();

	private static readonly Dictionary<ThingDef, List<ThingCategoryDef>> _ancestorCategories = new();

	private static readonly Dictionary<ThingCategoryDef, List<ThingDef>> _descendantThingDefs = new();

	private static readonly Dictionary<ThingCategoryDef, int> _unifiedStackLimits = new();

	private static bool _initialized;

	public static int StackLimitOf(ThingDef def) {
		Initialize();
		return _stackLimits.GetValueOrDefault(def, 1);
	}

	public static IReadOnlyList<ThingDef> DescendantThingDefsOf(ThingCategoryDef def) {
		Initialize();
		return _descendantThingDefs.TryGetValue(def, out var thingDefs) ? thingDefs : [];
	}

	public static IReadOnlyList<ThingCategoryDef> AncestorCategoriesOf(ThingDef def) {
		Initialize();
		return _ancestorCategories.TryGetValue(def, out var categories) ? categories : [];
	}

	public static bool Contains(ThingCategoryDef categoryDef, ThingDef thingDef) {
		Initialize();
		return _ancestorCategories.TryGetValue(thingDef, out var categories) && categories.Contains(categoryDef);
	}

	public static bool TryGetUnifiedStackLimit(ThingCategoryDef def, out int stackLimit) {
		Initialize();
		return _unifiedStackLimits.TryGetValue(def, out stackLimit);
	}

	private static void Initialize() {
		if (_initialized)
			return;
		_initialized = true;

		foreach (var categoryDef in DefDatabase<ThingCategoryDef>.AllDefsListForReading)
			_descendantThingDefs[categoryDef] = [];

		foreach (var thingDef in DefDatabase<ThingDef>.AllDefsListForReading) {
			_stackLimits[thingDef] = Math.Max(1, thingDef.stackLimit);
			var categories = new List<ThingCategoryDef>();
			foreach (var directCategory in thingDef.thingCategories ?? []) {
				var category = directCategory;
				while (category is not null) {
					if (!categories.Contains(category)) {
						categories.Add(category);
						if (!_descendantThingDefs.TryGetValue(category, out var descendants)) {
							descendants = [];
							_descendantThingDefs.Add(category, descendants);
						}
						descendants.Add(thingDef);
					}
					category = category.parent;
				}
			}
			if (categories.Count > 0)
				_ancestorCategories[thingDef] = categories;
		}

		foreach (var entry in _descendantThingDefs) {
			var stackLimit = -1;
			var unified = true;
			foreach (var thingDef in entry.Value) {
				var next = StackLimitOf(thingDef);
				if (stackLimit < 0) {
					stackLimit = next;
					continue;
				}
				if (stackLimit == next)
					continue;
				unified = false;
				break;
			}
			if (unified && stackLimit > 0)
				_unifiedStackLimits[entry.Key] = stackLimit;
		}
	}
}