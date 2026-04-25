using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage;

using static AmountUtility;

public sealed class Quota : IExposable {
	private ThingDef? _thingDef;

	private ThingCategoryDef? _categoryDef;

	private decimal _minStock = UNSET;

	private decimal _maxStock = UNSET;

	private string? _minStockValue;

	private string? _maxStockValue;

	private int _legacyMin = -1;

	private int _legacyMax = -1;

	public Quota() { }

	public Quota(ThingDef thingDef) => _thingDef = thingDef;

	public Quota(ThingCategoryDef categoryDef) => _categoryDef = categoryDef;

	public void ExposeData() {
		Scribe_Defs.Look(ref _thingDef, "thingDef");
		Scribe_Defs.Look(ref _categoryDef, "categoryDef");
		if (Scribe.mode == LoadSaveMode.Saving) {
			_minStockValue = Format(_minStock);
			_maxStockValue = Format(_maxStock);
		}
		Scribe_Values.Look(ref _minStockValue, "minStock");
		Scribe_Values.Look(ref _maxStockValue, "maxStock");
		if (Scribe.mode == LoadSaveMode.LoadingVars) {
			Scribe_Values.Look(ref _legacyMin, "min", -1);
			Scribe_Values.Look(ref _legacyMax, "max", -1);
		}
		if (Scribe.mode == LoadSaveMode.PostLoadInit) {
			_minStock = ParseSaved(_minStockValue, _legacyMin);
			_maxStock = ParseSaved(_maxStockValue, _legacyMax);
		}
	}

	public ThingDef? ThingDef => _thingDef;

	public ThingCategoryDef? CategoryDef => _categoryDef;

	public decimal MinStock {
		get => _minStock;
		set => _minStock = Normalize(value);
	}

	public decimal MaxStock {
		get => _maxStock;
		set => _maxStock = Normalize(value);
	}

	public bool HasMin => _minStock >= 0m;

	public bool HasMax => _maxStock >= 0m;

	public bool Active => HasMin || HasMax;

	public bool IsValidKey => _thingDef is not null || _categoryDef is not null;

	public bool ValidRange => !HasMin || !HasMax || _minStock <= _maxStock;

	public bool Effective => Active && IsValidKey && ValidRange;

	public string Key => _thingDef?.defName ?? $"Category:{_categoryDef?.defName}";

	public bool Matches(Thing thing) => Matches((thing.GetInnerIfMinified() ?? thing).def);

	public bool Matches(ThingDef def) {
		if (_thingDef is not null)
			return _thingDef == def;
		return _categoryDef is not null && DefCache.Contains(_categoryDef, def);
	}

	public Quota Clone() => new() {
		_thingDef = _thingDef,
		_categoryDef = _categoryDef,
		_minStock = _minStock,
		_maxStock = _maxStock
	};

	private decimal ParseSaved(string? value, int legacyValue) {
		if (!value.NullOrEmpty() && TryParse(value!, out var stock))
			return Normalize(stock);
		return LegacyToStock(legacyValue);
	}

	private decimal LegacyToStock(int value) {
		if (value < 0)
			return UNSET;
		if (_thingDef is not null)
			return RawToStock(value, _thingDef);
		if (_categoryDef is not null && DefCache.TryGetUnifiedStackLimit(_categoryDef, out var stackLimit))
			return RawToStock(value, stackLimit);
		return value;
	}
}