using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage;

public sealed class Profile(StorageSettings settings) : IExposable {
	public bool Enabled;

	public bool UseStockUnits;

	public bool SeparateLinkedStorages;

	private StorageSettings? _settings = settings;

	private readonly Dictionary<string, Quota> _quotas = new();

	public void ExposeData() {
		Scribe_Values.Look(ref Enabled, "enabled");
		Scribe_Values.Look(ref UseStockUnits, "useStockUnits");
		Scribe_Values.Look(ref SeparateLinkedStorages, "separateLinkedStorages");
		List<Quota>? quotas = null;
		if (Scribe.mode == LoadSaveMode.Saving) {
			PruneInactive();
			quotas = Quotas.ToList();
		}
		Scribe_Collections.Look(ref quotas, "quotas", LookMode.Deep);
		if (Scribe.mode == LoadSaveMode.PostLoadInit) {
			_quotas.Clear();
			foreach (var quota in quotas)
				_quotas[quota.Key] = quota;
			PruneInactive();
		}
	}

	public IReadOnlyCollection<Quota> Quotas => _quotas.Values;

	public bool HasData {
		get {
			if (Enabled || UseStockUnits || SeparateLinkedStorages)
				return true;
			foreach (var quota in _quotas.Values) {
				if (quota.Active)
					return true;
			}
			return false;
		}
	}

	public void Bind(StorageSettings settings) => _settings = settings;

	public ThingQuota GetOrCreateQuota(ThingDef def) {
		if (_quotas.TryGetValue(def.defName, out var quota))
			return (quota as ThingQuota)!;
		var newQuota = new ThingQuota(def);
		_quotas.Add(def.defName, newQuota);
		return newQuota;
	}

	public ThingCategoryQuota GetOrCreateQuota(ThingCategoryDef def) {
		if (_quotas.TryGetValue(def.defName, out var quota))
			return (quota as ThingCategoryQuota)!;
		var newQuota = new ThingCategoryQuota(def);
		_quotas.Add(def.defName, newQuota);
		return newQuota;
	}

	public IEnumerable<Quota> MatchingQuotas(Thing thing) => MatchingQuotas((thing.GetInnerIfMinified() ?? thing).def);

	public IEnumerable<Quota> MatchingQuotas(ThingDef def) {
		if (_quotas.TryGetValue(def.defName, out var thingQuota) && QuotaValid(thingQuota))
			yield return thingQuota;
		foreach (var category in DefCache.AncestorCategoriesOf(def)) {
			if (_quotas.TryGetValue(category.defName, out var categoryQuota) && QuotaValid(categoryQuota))
				yield return categoryQuota;
		}
	}

	public bool QuotaValid(Quota quota) => QuotaLocallyUsable(quota) && CategoryTotalsValid(quota);

	public bool CategoryTotalsValid(Quota quota) {
		if (quota is not ThingCategoryQuota { CategoryDef: { } categoryDef })
			return true;
		if (quota.HasMin && quota.MinStock < CategoryChildSum(categoryDef, false))
			return false;
		return !quota.HasMax || quota.MaxStock >= CategoryChildSum(categoryDef, true);
	}

	public void PruneInactive() => _quotas.RemoveAll(pair => !pair.Value.Active || !pair.Value.Valid);

	public Profile CloneFor(StorageSettings settings) {
		var clone = new Profile(settings) {
			Enabled = Enabled,
			UseStockUnits = UseStockUnits,
			SeparateLinkedStorages = SeparateLinkedStorages
		};
		foreach ((string? def, var quota) in _quotas)
			clone._quotas.Add(def, quota.Clone());
		return clone;
	}

	internal decimal CountFor(Quota quota, ISlotGroupParent? parent = null) {
		if (_settings is null)
			return 0m;
		var count = 0m;
		foreach (var thing in _settings.HeldThings(parent)) {
			if (quota.Matches(thing))
				count += CountStock(thing);
		}
		return count;
	}

	private static decimal CountStock(Thing thing) => AmountUtility.RawToStock(thing.stackCount, (thing.GetInnerIfMinified() ?? thing).def);

	private static decimal? ChildContribution(Quota quota, bool max) {
		if (max) {
			if (quota.HasMax)
				return quota.MaxStock;
			if (quota.HasMin)
				return quota.MinStock;
			return null;
		}
		return quota.HasMin ? quota.MinStock : null;
	}

	private bool QuotaLocallyUsable(Quota quota) {
		if (!quota.Effective)
			return false;
		if (quota is ThingQuota)
			return true;
		return UseStockUnits || quota is ThingCategoryQuota { CategoryDef: { } categoryDef } && DefCache.TryGetUnifiedStackLimit(categoryDef, out _);
	}

	private decimal CategoryChildSum(ThingCategoryDef categoryDef, bool max) {
		var sum = 0m;
		foreach (var thingDef in DefCache.DirectThingDefsOf(categoryDef)) {
			if (
				_quotas.TryGetValue(thingDef.defName, out var thingQuota)
				&& QuotaLocallyUsable(thingQuota)
				&& ChildContribution(thingQuota, max) is { } contribution
			)
				sum += contribution;
		}
		foreach (var childCategory in DefCache.ChildCategoriesOf(categoryDef)) {
			if (
				_quotas.TryGetValue(childCategory.defName, out var categoryQuota)
				&& QuotaLocallyUsable(categoryQuota)
				&& ChildContribution(categoryQuota, max) is { } contribution
			)
				sum += contribution;
			else
				sum += CategoryChildSum(childCategory, max);
		}
		return sum;
	}
}