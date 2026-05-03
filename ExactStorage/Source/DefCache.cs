using System;
using System.Collections.Generic;
using System.Linq;
using TrueMogician.Extensions.Collections.Tree;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage;

[StaticConstructorOnStartup]
public static class DefCache {
	private static readonly Dictionary<ThingCategoryDef, ValuedTreeNode<Def>> _categoryNodes = [];

	static DefCache() {
		var categories = DefDatabase<ThingCategoryDef>.AllDefsListForReading;
		foreach (var category in categories) {
			if (!_categoryNodes.ContainsKey(category))
				_categoryNodes.Add(category, new ValuedTreeNode<Def>(category));
		}
		var forest = TreeUtilities.BuildForest(categories, c => _categoryNodes[c], c => c.parent is { } p ? _categoryNodes[p] : null);
		RootCategoryDefs = forest.Select(n => (ThingCategoryDef)n.Root.Value).ToList();

		foreach (var thingDef in DefDatabase<ThingDef>.AllDefsListForReading) {
			var thingCategories = new HashSet<ThingCategoryDef>(thingDef.thingCategories ?? []);
			foreach (var category in thingCategories) {
				if (_categoryNodes.TryGetValue(category, out var node))
					node.Children.Add(new ValuedTreeNode<Def>(thingDef));
			}
		}
	}

	public static IReadOnlyList<ThingCategoryDef> RootCategoryDefs { get; private set; }

	public static IEnumerable<ThingCategoryDef> AncestorCategoriesOf(ThingCategoryDef def) {
		var current = def.parent;
		while (current is not null) {
			yield return current;
			current = current.parent;
		}
	}

	public static IEnumerable<ThingCategoryDef> AncestorCategoriesOf(ThingDef def) {
		foreach (var category in def.thingCategories ?? []) {
			if (!_categoryNodes.TryGetValue(category, out var node) || node.Value != category)
				continue;
			yield return category;
			foreach (var ancestor in node.Ancestors)
				yield return (ancestor.Value as ThingCategoryDef)!;
		}
	}

	public static IEnumerable<Def> ChildrenOf(ThingCategoryDef def) =>
		!_categoryNodes.TryGetValue(def, out var node) ? [] : node.Children.Select(c => c.Value);

	public static IEnumerable<ThingCategoryDef> ChildCategoriesOf(ThingCategoryDef def) {
		if (!_categoryNodes.TryGetValue(def, out var node))
			yield break;
		foreach (var child in node.Children) {
			if (child.Value is ThingCategoryDef categoryDef)
				yield return categoryDef;
		}
	}

	public static IEnumerable<ThingDef> ChildThingsOf(ThingCategoryDef def) {
		if (!_categoryNodes.TryGetValue(def, out var node))
			yield break;
		foreach (var child in node.Children) {
			if (child.Value is ThingDef thingDef)
				yield return thingDef;
		}
	}

	public static IEnumerable<ThingDef> DescendantThingsOf(ThingCategoryDef def) {
		if (!_categoryNodes.TryGetValue(def, out var node))
			yield break;
		foreach (var thingDef in ThingDefsUnder(node))
			yield return thingDef;
	}

	public static bool Contains(ThingCategoryDef categoryDef, ThingDef thingDef) {
		foreach (var category in AncestorCategoriesOf(thingDef)) {
			if (category == categoryDef)
				return true;
		}
		return false;
	}

	public static bool TryGetUnifiedStackLimit(ThingCategoryDef def, out int stackLimit) {
		if (_categoryNodes.TryGetValue(def, out var node) && TryGetUnifiedStackLimit(node, out stackLimit))
			return true;
		stackLimit = 0;
		return false;
	}

	private static IEnumerable<ThingDef> ThingDefsUnder(ValuedTreeNode<Def> node) {
		foreach (var descendant in node.Descendants.Distinct()) {
			if (descendant.Value is ThingDef thingDef)
				yield return thingDef;
		}
	}

	private static bool TryGetUnifiedStackLimit(ValuedTreeNode<Def> node, out int stackLimit) {
		stackLimit = -1;
		var unified = true;
		foreach (var thingDef in ThingDefsUnder(node)) {
			var next = Math.Max(1, thingDef.stackLimit);
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
			return true;
		stackLimit = 0;
		return false;
	}
}