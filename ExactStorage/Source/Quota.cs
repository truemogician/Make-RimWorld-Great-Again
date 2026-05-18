using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage;

using static AmountUtility;

public abstract class Quota : IExposable {
	private decimal _min = UNSET;

	private decimal _max = UNSET;

	public virtual void ExposeData() {
		string? min = null, max = null;
		if (Scribe.mode == LoadSaveMode.Saving) {
			min = Format(_min);
			max = Format(_max);
		}
		Scribe_Values.Look(ref min, "min");
		Scribe_Values.Look(ref max, "max");
		if (Scribe.mode == LoadSaveMode.LoadingVars) {
			_min = ParseSaved(min);
			_max = ParseSaved(max);
		}
	}

	public abstract string Key { get; }

	public decimal Min {
		get => _min;
		set => _min = Normalize(value);
	}

	public decimal Max {
		get => _max;
		set => _max = Normalize(value);
	}

	public bool HasMin => _min >= 0m;

	public bool HasMax => _max >= 0m;

	public bool Active => HasMin || HasMax;

	public abstract bool Valid { get; }

	public bool ValidRange => !HasMin || !HasMax || _min <= _max;

	public bool Effective => Valid && Active && ValidRange;

	public bool Matches(Thing thing) => Matches(thing.InnerDef);

	public abstract bool Matches(ThingDef def);

	public Quota Clone() => (Quota)MemberwiseClone();

	private static decimal ParseSaved(string? value) {
		if (!value.NullOrEmpty() && TryParse(value!, out var stack))
			return Normalize(stack);
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
		ThingDefs = DefCache.DescendantThingsOf(categoryDef).ToList();
	}

	public override string Key => _categoryDef?.defName ?? throw new InvalidOperationException("ThingCategoryQuota has no valid ThingCategoryDef");

	public override bool Valid => _categoryDef is not null;

	public ThingCategoryDef? CategoryDef => _categoryDef;

	public override void ExposeData() {
		Scribe_Defs.Look(ref _categoryDef, "categoryDef");
		if (Scribe.mode == LoadSaveMode.PostLoadInit && _categoryDef is not null)
			ThingDefs = DefCache.DescendantThingsOf(_categoryDef).ToList();
		base.ExposeData();
	}
}