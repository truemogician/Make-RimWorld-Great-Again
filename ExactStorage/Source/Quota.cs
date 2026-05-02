using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage;

using static AmountUtility;

public abstract class Quota : IExposable {
	private decimal _minStock = UNSET;

	private decimal _maxStock = UNSET;

	public virtual void ExposeData() {
		string? minStock = null, maxStock = null;
		if (Scribe.mode == LoadSaveMode.Saving) {
			minStock = Format(_minStock);
			maxStock = Format(_maxStock);
		}
		Scribe_Values.Look(ref minStock, "minStock");
		Scribe_Values.Look(ref maxStock, "maxStock");
		if (Scribe.mode == LoadSaveMode.LoadingVars) {
			_minStock = ParseSaved(minStock);
			_maxStock = ParseSaved(maxStock);
		}
	}

	public abstract string Key { get; }

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

	public abstract bool Valid { get; }

	public bool ValidRange => !HasMin || !HasMax || _minStock <= _maxStock;

	public bool Effective => Valid && Active && ValidRange;

	public bool Matches(Thing thing) => Matches((thing.GetInnerIfMinified() ?? thing).def);

	public abstract bool Matches(ThingDef def);

	public Quota Clone() => (Quota)MemberwiseClone();

	private static decimal ParseSaved(string? value) {
		if (!value.NullOrEmpty() && TryParse(value!, out var stock))
			return Normalize(stock);
		return UNSET;
	}
}

public class ThingQuota : Quota {
	private ThingDef? _thingDef;

	public ThingQuota() { }

	public ThingQuota(ThingDef thingDef) => _thingDef = thingDef;

	public override string Key => _thingDef?.defName ?? throw new InvalidOperationException("ThingQuota has no valid ThingDef");

	public override bool Valid => _thingDef is not null;

	public ThingDef? ThingDef => _thingDef;

	public override void ExposeData() {
		Scribe_Defs.Look(ref _thingDef, "thingDef");
		base.ExposeData();
	}

	public override bool Matches(ThingDef def) => _thingDef == def || _thingDef?.defName == def.defName;
}

public abstract class ThingGroupQuota : Quota {
	private HashSet<string> _thingDefNames = [];

	public IReadOnlyCollection<ThingDef> ThingDefs {
		get;
		protected set {
			field = value;
			_thingDefNames = [.. value.Select(t => t.defName)];
		}
	} = [];

	public override bool Matches(ThingDef def) => _thingDefNames.Contains(def.defName);
}

public class ThingCategoryQuota : ThingGroupQuota {
	private ThingCategoryDef? _categoryDef;

	public ThingCategoryQuota() { }

	public ThingCategoryQuota(ThingCategoryDef categoryDef) {
		_categoryDef = categoryDef;
		ThingDefs = DefCache.DescendantThingDefsOf(categoryDef);
	}

	public override string Key => _categoryDef?.defName ?? throw new InvalidOperationException("ThingCategoryQuota has no valid ThingCategoryDef");

	public override bool Valid => _categoryDef is not null;

	public ThingCategoryDef? CategoryDef => _categoryDef;

	public override void ExposeData() {
		Scribe_Defs.Look(ref _categoryDef, "categoryDef");
		if (Scribe.mode == LoadSaveMode.PostLoadInit && _categoryDef is not null)
			ThingDefs = DefCache.DescendantThingDefsOf(_categoryDef);
		base.ExposeData();
	}
}