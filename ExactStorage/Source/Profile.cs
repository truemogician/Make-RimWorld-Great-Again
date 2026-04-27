using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage;

using static StorageUtility;

public sealed class Profile(StorageSettings settings) : IExposable {
	public bool Enabled;

	public bool UseStockUnits;

	public bool SeparateLinkedStorages;

	private StorageSettings? _settings = settings;

	private readonly Dictionary<ThingDef, Quota> _thingQuotas = new();

	private readonly Dictionary<ThingCategoryDef, Quota> _categoryQuotas = new();

	public void ExposeData() {
		Scribe_Values.Look(ref Enabled, "enabled");
		Scribe_Values.Look(ref UseStockUnits, "useStockUnits");
		Scribe_Values.Look(ref SeparateLinkedStorages, "separateLinkedStorages");
		var flat = Scribe.mode == LoadSaveMode.Saving ? FlattenQuotas() : null;
		Scribe_Collections.Look(ref flat, "quotas", LookMode.Deep);
		if (Scribe.mode == LoadSaveMode.PostLoadInit) {
			RebuildIndices(flat);
			PruneInactive();
		}
	}

	public IEnumerable<Quota> Quotas => _thingQuotas.Values.Concat(_categoryQuotas.Values);

	public bool HasData {
		get {
			if (Enabled || UseStockUnits || SeparateLinkedStorages)
				return true;
			foreach (var quota in _thingQuotas.Values) {
				if (quota.Active)
					return true;
			}
			foreach (var quota in _categoryQuotas.Values) {
				if (quota.Active)
					return true;
			}
			return false;
		}
	}

	public void Bind(StorageSettings settings) => _settings = settings;

	public Quota? GetQuota(ThingDef def, bool create = false) {
		if (_thingQuotas.TryGetValue(def, out var quota))
			return quota;
		if (!create)
			return null;
		quota = new Quota(def);
		_thingQuotas.Add(def, quota);
		return quota;
	}

	public Quota? GetQuota(ThingCategoryDef def, bool create = false) {
		if (_categoryQuotas.TryGetValue(def, out var quota))
			return quota;
		if (!create)
			return null;
		quota = new Quota(def);
		_categoryQuotas.Add(def, quota);
		return quota;
	}

	public List<Quota> MatchingQuotas(Thing thing) => MatchingQuotas((thing.GetInnerIfMinified() ?? thing).def);

	public List<Quota> MatchingQuotas(ThingDef def) {
		var result = new List<Quota>();
		if (_thingQuotas.TryGetValue(def, out var thingQuota) && QuotaUsable(thingQuota))
			result.Add(thingQuota);
		foreach (var category in DefCache.AncestorCategoriesOf(def)) {
			if (_categoryQuotas.TryGetValue(category, out var categoryQuota) && QuotaUsable(categoryQuota))
				result.Add(categoryQuota);
		}
		return result;
	}

	public bool QuotaUsable(Quota quota) {
		if (!quota.Effective)
			return false;
		if (quota.ThingDef is not null)
			return true;
		return UseStockUnits || quota.CategoryDef is not null && DefCache.TryGetUnifiedStackLimit(quota.CategoryDef, out _);
	}

	public decimal CountFor(Quota quota, ISlotGroupParent? parent = null) => CountFor(quota, parent, null);

	public void PruneInactive() {
		PruneInactive(_thingQuotas);
		PruneInactive(_categoryQuotas);
	}

	public Profile CloneFor(StorageSettings settings) {
		var clone = new Profile(settings) {
			Enabled = Enabled,
			UseStockUnits = UseStockUnits,
			SeparateLinkedStorages = SeparateLinkedStorages
		};
		foreach (var (def, quota) in _thingQuotas)
			clone._thingQuotas.Add(def, quota.Clone());
		foreach (var (def, quota) in _categoryQuotas)
			clone._categoryQuotas.Add(def, quota.Clone());
		return clone;
	}

	internal decimal CountFor(Quota quota, ISlotGroupParent? parent, StorageEvaluationCache? cache) {
		if (cache is not null && _settings is not null)
			return cache.CountFor(_settings, quota, parent);

		return CountForSlow(quota, parent);
	}

	private static void PruneInactive<TKey>(Dictionary<TKey, Quota> dict) where TKey : notnull {
		List<TKey>? toRemove = null;
		foreach (var (key, quota) in dict) {
			if (quota.Active && quota.IsValidKey)
				continue;
			toRemove ??= [];
			toRemove.Add(key);
		}
		if (toRemove is null)
			return;
		foreach (var key in toRemove)
			dict.Remove(key);
	}

	private static decimal CountStock(Thing thing) => AmountUtility.RawToStock(thing.stackCount, (thing.GetInnerIfMinified() ?? thing).def);

	private List<Quota> FlattenQuotas() {
		var flat = new List<Quota>(_thingQuotas.Count + _categoryQuotas.Count);
		foreach (var quota in _thingQuotas.Values)
			flat.Add(quota);
		foreach (var quota in _categoryQuotas.Values)
			flat.Add(quota);
		return flat;
	}

	private void RebuildIndices(List<Quota>? flat) {
		_thingQuotas.Clear();
		_categoryQuotas.Clear();
		if (flat is null)
			return;
		foreach (var quota in flat) {
			if (quota.ThingDef is { } thingDef)
				_thingQuotas[thingDef] = quota;
			else if (quota.CategoryDef is { } categoryDef)
				_categoryQuotas[categoryDef] = quota;
		}
	}

	private decimal CountForSlow(Quota quota, ISlotGroupParent? parent) {
		var count = 0m;
		foreach (var thing in HeldThings(_settings, parent)) {
			if (quota.Matches(thing))
				count += CountStock(thing);
		}
		return count;
	}
}