using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Rimsonable.Static;
using TrueMogician.RimWorld.Utility;
using TrueMogician.RimWorld.Utility.Attributes;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.Rimsonable.Patches;

using IngredientProvider = Func<Building_NutrientPasteDispenser, CompIngredients, bool>;

public static class IngredientAwareNutrientPastePolicies {
	private static readonly AssignmentClosureFinder _foodValidatorFinder = new(IsFoodValidatorAssignment);

	private static readonly Dictionary<Type, FoodValidatorFields> _foodValidatorFields = new();

	private static readonly List<IngredientProvider> _ingredientProviders = [CollectIngredientsFromHoppers];

	private static readonly MethodInfo _foodValidatorTranspiler = AccessTools.Method(
		typeof(IngredientAwareNutrientPastePolicies),
		nameof(FoodValidator_Transpiler)
	)!;

	private static MethodBase? _foodValidator;

	public static void AddIngredientProvider(Func<Building_NutrientPasteDispenser, CompIngredients, bool> provider) {
		if (!_ingredientProviders.Contains(provider))
			_ingredientProviders.Add(provider);
	}

	[HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.BestFoodSourceOnMap))]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> Inspect(IEnumerable<CodeInstruction> insts) => _foodValidatorFinder.Transpile(insts);

	[PatchHook(PatchHookTiming.BeforeUnpatch)]
	internal static void BeforeUnpatch(Harmony harmony) {
		if (_foodValidator is null)
			return;
		harmony.Unpatch(_foodValidator, _foodValidatorTranspiler);
		_foodValidator = null;
	}

	[PatchHook(PatchHookTiming.AfterPatch)]
	internal static void AfterPatch(Harmony harmony) {
		switch (_foodValidatorFinder.Closures.Count) {
			case 0: Helper.Logger.Error("Failed to find FoodUtility.BestFoodSourceOnMap food validator.", true); break;
			case 1 when _foodValidator != _foodValidatorFinder.Closures[0]:
				BeforeUnpatch(harmony);
				_foodValidator = _foodValidatorFinder.Closures[0];
				harmony.Patch(_foodValidator, transpiler: new HarmonyMethod(_foodValidatorTranspiler));
				break;
			case > 1: Helper.Logger.Error("Found multiple FoodUtility.BestFoodSourceOnMap food validators.", true); break;
		}
	}

	internal static IEnumerable<CodeInstruction> FoodValidator_Transpiler(IEnumerable<CodeInstruction> insts) {
		var willEatThingDef = AccessTools.Method(
			typeof(FoodUtility),
			nameof(FoodUtility.WillEat),
			[typeof(Pawn), typeof(ThingDef), typeof(Pawn), typeof(bool), typeof(bool)]
		)!;
		var validate = AccessTools.Method(typeof(IngredientAwareNutrientPastePolicies), nameof(AllowsFoodSourceIngredients))!;
		var injected = false;
		foreach (var inst in insts) {
			yield return inst;
			if (!injected && inst.Calls(willEatThingDef)) {
				yield return new CodeInstruction(OpCodes.Ldarg_0);
				yield return new CodeInstruction(OpCodes.Ldarg_1);
				yield return new CodeInstruction(OpCodes.Call, validate);
				injected = true;
			}
		}
		if (!injected)
			Helper.Logger.Error("Failed to add ingredient-aware nutrient paste validation to FoodUtility.BestFoodSourceOnMap.");
	}

	internal static bool AllowsFoodSourceIngredients(bool vanillaAllows, object foodValidatorState, Thing foodSource) {
		if (!vanillaAllows || foodSource is not Building_NutrientPasteDispenser dispenser)
			return vanillaAllows;
		var fields = GetFoodValidatorFields(foodValidatorState.GetType());
		if (!fields.Valid)
			return vanillaAllows;
		return AllowsDispenserIngredients(
			(Pawn)fields.Eater!.GetValue(foodValidatorState),
			(Pawn)fields.Getter!.GetValue(foodValidatorState),
			dispenser,
			(bool)fields.AllowVenerated!.GetValue(foodValidatorState)
		);
	}

	internal static bool AllowsDispenserIngredients(
		Pawn eater,
		Pawn getter,
		Building_NutrientPasteDispenser dispenser,
		bool allowVenerated
	) {
		var paste = ThingMaker.MakeThing(dispenser.DispensableDef);
		try {
			if (paste.TryGetComp<CompIngredients>() is not { } compIngredients)
				return true;
			RegisterDispensedIngredients(dispenser, compIngredients);
			return eater.WillEat(paste, getter, true, allowVenerated);
		}
		finally {
			if (!paste.Destroyed)
				paste.Destroy();
		}
	}

	private static bool IsFoodValidatorAssignment(MemberInfo member) {
		if (member is not FieldInfo { FieldType: { } fieldType, DeclaringType: { } declaringType })
			return false;
		if (fieldType != typeof(Predicate<Thing>))
			return false;
		return AccessTools.Field(declaringType, "eater")?.FieldType == typeof(Pawn)
			&& AccessTools.Field(declaringType, "getter")?.FieldType == typeof(Pawn)
			&& AccessTools.Field(declaringType, "allowVenerated")?.FieldType == typeof(bool);
	}

	private static void RegisterDispensedIngredients(Building_NutrientPasteDispenser dispenser, CompIngredients compIngredients) {
		for (var i = _ingredientProviders.Count - 1; i >= 0; i--) {
			try {
				if (_ingredientProviders[i](dispenser, compIngredients))
					return;
			}
			catch (Exception ex) {
				Helper.Logger.Error($"Failed to collect nutrient paste dispenser ingredients from provider #{i}: {ex}");
			}
		}
	}

	private static bool CollectIngredientsFromHoppers(Building_NutrientPasteDispenser dispenser, CompIngredients compIngredients) {
		var ingredients = new List<ThingDef>();
		var remainingNutrition = dispenser.def.building.nutritionCostPerDispense - 0.0001f;
		var remainingStacks = new Dictionary<Thing, int>();
		while (remainingNutrition > 0f) {
			var feedstock = FindFeedstockInAnyHopper(dispenser, remainingStacks);
			if (feedstock is null)
				break;
			var nutrition = feedstock.GetStatValue(StatDefOf.Nutrition);
			if (nutrition <= 0f)
				break;
			var availableStack = RemainingStack(feedstock, remainingStacks);
			var count = Mathf.Min(availableStack, Mathf.CeilToInt(remainingNutrition / nutrition));
			if (count <= 0)
				break;
			remainingStacks[feedstock] = availableStack - count;
			remainingNutrition -= count * nutrition;
			ingredients.Add(feedstock.def);
		}
		if (remainingNutrition > 0f)
			return false;
		foreach (var ingredient in ingredients)
			compIngredients.RegisterIngredient(ingredient);
		return true;
	}

	private static Thing? FindFeedstockInAnyHopper(Building_NutrientPasteDispenser dispenser, Dictionary<Thing, int> remainingStacks) {
		for (var i = 0; i < dispenser.AdjCellsCardinalInBounds.Count; i++) {
			Thing? feedstock = null;
			var hasHopper = false;
			var thingList = dispenser.AdjCellsCardinalInBounds[i].GetThingList(dispenser.Map);
			foreach (var thing in thingList) {
				if (Building_NutrientPasteDispenser.IsAcceptableFeedstock(thing.def) && RemainingStack(thing, remainingStacks) > 0)
					feedstock = thing;
				if (thing.IsHopper())
					hasHopper = true;
			}
			if (feedstock is not null && hasHopper)
				return feedstock;
		}
		return null;
	}

	private static int RemainingStack(Thing thing, Dictionary<Thing, int> remainingStacks) =>
		remainingStacks.TryGetValue(thing, out var remainingStack) ? remainingStack : thing.stackCount;

	private static FoodValidatorFields GetFoodValidatorFields(Type type) {
		if (_foodValidatorFields.TryGetValue(type, out var fields))
			return fields;
		fields = new FoodValidatorFields(
			AccessTools.Field(type, "eater"),
			AccessTools.Field(type, "getter"),
			AccessTools.Field(type, "allowVenerated")
		);
		if (!fields.Valid)
			Helper.Logger.Error("Failed to access FoodUtility.BestFoodSourceOnMap food validator fields.");
		_foodValidatorFields[type] = fields;
		return fields;
	}

	private sealed class FoodValidatorFields(FieldInfo? eater, FieldInfo? getter, FieldInfo? allowVenerated) {
		public readonly FieldInfo? Eater = eater;
		public readonly FieldInfo? Getter = getter;
		public readonly FieldInfo? AllowVenerated = allowVenerated;

		public bool Valid => Eater is not null && Getter is not null && AllowVenerated is not null;
	}
}