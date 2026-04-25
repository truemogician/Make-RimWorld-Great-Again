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

	private List<Quota> _quotas = [];

	public void ExposeData() {
		Scribe_Values.Look(ref Enabled, "enabled");
		Scribe_Values.Look(ref UseStockUnits, "useStockUnits");
		Scribe_Values.Look(ref SeparateLinkedStorages, "separateLinkedStorages");
		Scribe_Collections.Look(ref _quotas, "quotas", LookMode.Deep);
		_quotas ??= [];
		if (Scribe.mode == LoadSaveMode.PostLoadInit)
			PruneInactive();
	}

	public IReadOnlyList<Quota> Quotas => _quotas;

	public bool HasData => Enabled || UseStockUnits || SeparateLinkedStorages || _quotas.Any(q => q is { Active: true, IsValidKey: true });

	public void Bind(StorageSettings settings) => _settings = settings;

	public Quota? GetQuota(ThingDef def, bool create = false) {
		var quota = _quotas.FirstOrDefault(q => q.ThingDef == def);
		if (quota is null && create) {
			quota = new Quota(def);
			_quotas.Add(quota);
		}
		return quota;
	}

	public Quota? GetQuota(ThingCategoryDef def, bool create = false) {
		var quota = _quotas.FirstOrDefault(q => q.CategoryDef == def);
		if (quota is null && create) {
			quota = new Quota(def);
			_quotas.Add(quota);
		}
		return quota;
	}

	public List<Quota> MatchingQuotas(Thing thing) => MatchingQuotas((thing.GetInnerIfMinified() ?? thing).def);

	public List<Quota> MatchingQuotas(ThingDef def) {
		var result = new List<Quota>();
		foreach (var quota in _quotas) {
			if (QuotaUsable(quota) && quota.Matches(def))
				result.Add(quota);
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

	public void PruneInactive() => _quotas.RemoveAll(q => !q.Active || !q.IsValidKey);

	public Profile CloneFor(StorageSettings settings) {
		return new Profile(settings) {
			Enabled = Enabled,
			UseStockUnits = UseStockUnits,
			SeparateLinkedStorages = SeparateLinkedStorages,
			_quotas = _quotas.Select(q => q.Clone()).ToList()
		};
	}

	internal decimal CountFor(Quota quota, ISlotGroupParent? parent, StorageEvaluationCache? cache) {
		if (cache is not null && _settings is not null)
			return cache.CountFor(_settings, quota, parent);

		return CountForSlow(quota, parent);
	}

	private static decimal CountStock(Thing thing) => AmountUtility.RawToStock(thing.stackCount, (thing.GetInnerIfMinified() ?? thing).def);

	private decimal CountForSlow(Quota quota, ISlotGroupParent? parent) {
		var count = 0m;
		foreach (var thing in HeldThings(_settings, parent)) {
			if (quota.Matches(thing))
				count += CountStock(thing);
		}
		return count;
	}
}