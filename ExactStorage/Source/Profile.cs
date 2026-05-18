using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage;

using static AmountUtility;

public sealed class Profile(StorageSettings settings) : IExposable {
	private bool _enabled;

	private bool _useStackUnit;

	private bool _separateLinkedStorages;

	private readonly Dictionary<string, Quota> _quotas = new();

	public void ExposeData() {
		Scribe_Values.Look(ref _enabled, "enabled");
		Scribe_Values.Look(ref _useStackUnit, "useStackUnit");
		Scribe_Values.Look(ref _separateLinkedStorages, "separateLinkedStorages");
		List<Quota>? quotas = null;
		if (Scribe.mode == LoadSaveMode.Saving) {
			PruneInactive();
			quotas = Quotas.ToList();
		}
		Scribe_Collections.Look(ref quotas, "quotas", LookMode.Deep);
		if (Scribe.mode == LoadSaveMode.LoadingVars) {
			_quotas.Clear();
			foreach (var quota in quotas)
				_quotas[quota.Key] = quota;
			PruneInactive();
			Manager.BumpProfileVersion();
		}
	}

	public StorageSettings Settings { get; internal set; } = settings;

	public bool Enabled {
		get => _enabled;
		set {
			if (_enabled == value)
				return;
			_enabled = value;
			Manager.BumpProfileVersion();
		}
	}

	public bool UseStackUnit {
		get => _useStackUnit;
		set {
			if (_useStackUnit == value)
				return;
			_useStackUnit = value;
			Manager.BumpProfileVersion();
		}
	}

	public bool SeparateLinkedStorages {
		get => _separateLinkedStorages;
		set {
			if (_separateLinkedStorages == value)
				return;
			_separateLinkedStorages = value;
			Manager.BumpProfileVersion();
		}
	}

	public IReadOnlyCollection<Quota> Quotas => _quotas.Values;

	public bool HasData {
		get {
			if (_enabled || _useStackUnit || _separateLinkedStorages)
				return true;
			foreach (var quota in _quotas.Values) {
				if (quota.Active)
					return true;
			}
			return false;
		}
	}

	public static void SetMin(Quota quota, decimal value) {
		if (quota.Min == value)
			return;
		quota.Min = value;
		Manager.BumpProfileVersion();
	}

	public static void SetMax(Quota quota, decimal value) {
		if (quota.Max == value)
			return;
		quota.Max = value;
		Manager.BumpProfileVersion();
	}

	public ThingQuota GetOrCreateQuota(ThingDef def) {
		if (_quotas.TryGetValue(def.defName, out var quota))
			return (quota as ThingQuota)!;
		var newQuota = new ThingQuota(def);
		_quotas.Add(def.defName, newQuota);
		Manager.BumpProfileVersion();
		return newQuota;
	}

	public ThingCategoryQuota GetOrCreateQuota(ThingCategoryDef def) {
		if (_quotas.TryGetValue(def.defName, out var quota))
			return (quota as ThingCategoryQuota)!;
		var newQuota = new ThingCategoryQuota(def);
		_quotas.Add(def.defName, newQuota);
		Manager.BumpProfileVersion();
		return newQuota;
	}

	public IEnumerable<Quota> MatchingQuotas(Thing thing) => MatchingQuotas(thing.InnerDef);

	public IEnumerable<Quota> MatchingQuotas(ThingDef def) {
		if (_quotas.TryGetValue(def.defName, out var thingQuota) && QuotaValid(thingQuota))
			yield return thingQuota;
		foreach (var category in DefCache.AncestorCategoriesOf(def)) {
			if (_quotas.TryGetValue(category.defName, out var categoryQuota) && QuotaValid(categoryQuota))
				yield return categoryQuota;
		}
	}

	public bool QuotaValid(Quota quota) => QuotaUsable(quota) && CategoryTotalsValid(quota);

	public bool HasActiveAncestorCategoryQuota(Quota quota, StorageSettings settings) {
		var ancestors = quota switch {
			ThingQuota { ThingDef: { } thingDef }               => DefCache.AncestorCategoriesOf(thingDef),
			ThingCategoryQuota { CategoryDef: { } categoryDef } => DefCache.AncestorCategoriesOf(categoryDef),
			_                                                   => []
		};
		foreach (var category in ancestors) {
			if (_quotas.TryGetValue(category.defName, out var ancestor) && QuotaValid(ancestor) && settings.QuotaAllowed(ancestor))
				return true;
		}
		return false;
	}

	public bool CategoryTotalsValid(Quota quota) {
		if (quota is not ThingCategoryQuota { CategoryDef: { } categoryDef })
			return true;
		if (quota.HasMin) {
			if (quota.Min < CategoryChildrenSlots(categoryDef, false))
				return false;
			if (CategoryMinExceedsMaxBound(quota))
				return false;
		}
		return !quota.HasMax || quota.Max >= CategoryChildrenSlots(categoryDef, true);
	}

	public bool CategoryMinExceedsMaxBound(Quota quota) =>
		quota is ThingCategoryQuota { CategoryDef: { } categoryDef }
		&& quota.HasMin
		&& CategoryMaxBound(categoryDef) is { } cap
		&& quota.Min > cap;

	public void PruneInactive() {
		int removed = _quotas.RemoveAll(pair => !pair.Value.Active || !pair.Value.Valid);
		if (removed > 0)
			Manager.BumpProfileVersion();
	}

	public Profile CloneFor(StorageSettings settings) {
		var clone = new Profile(settings) {
			_enabled = _enabled,
			_useStackUnit = _useStackUnit,
			_separateLinkedStorages = _separateLinkedStorages
		};
		foreach ((string? def, var quota) in _quotas)
			clone._quotas.Add(def, quota.Clone());
		return clone;
	}

	internal decimal CountFor(Quota quota, ISlotGroupParent? parent = null) {
		var count = 0m;
		foreach (var thing in Settings.HeldThings(parent)) {
			if (quota.Matches(thing))
				count += RawToStack(thing.stackCount, thing.InnerDef);
		}
		return count;
	}

	internal uint CategoryChildrenSlots(ThingCategoryDef categoryDef, bool max) {
		var sum = 0u;
		foreach (var def in DefCache.ChildrenOf(categoryDef)) {
			if (_quotas.TryGetValue(def.defName, out var quota)
				&& QuotaUsable(quota)
				&& Settings.QuotaAllowed(quota)
				&& ChildContrib(quota, max, def is ThingCategoryDef) is { } contrib)
				sum += (uint)Math.Ceiling(contrib); // Different items can't stack together
			else if (def is ThingCategoryDef catDef)
				sum += CategoryChildrenSlots(catDef, max);
		}
		return sum;
	}

	private static decimal? ChildContrib(Quota quota, bool max, bool category) {
		if (max) {
			if (quota.HasMax)
				return quota.Max;
			if (category)
				return null;
		}
		return quota.HasMin ? quota.Min : null;
	}

	private decimal? CategoryMaxBound(ThingCategoryDef categoryDef) {
		var sum = 0m;
		foreach (var def in DefCache.ChildrenOf(categoryDef)) {
			if (def is ThingDef thing && !Settings.filter.Allows(thing))
				continue;
			if (_quotas.TryGetValue(def.defName, out var quota) && QuotaUsable(quota)) {
				if (quota.HasMax)
					sum += quota.Max;
				else
					return null;
			}
			else if (def is ThingCategoryDef category && CategoryMaxBound(category) is { } childSum)
				sum += childSum;
			else
				return null;
		}
		return sum;
	}

	private bool QuotaUsable(Quota quota) {
		if (!quota.Effective)
			return false;
		if (quota is ThingQuota)
			return true;
		return UseStackUnit || quota is ThingCategoryQuota { CategoryDef: { } categoryDef } && DefCache.TryGetUnifiedStackLimit(categoryDef, out _);
	}
}