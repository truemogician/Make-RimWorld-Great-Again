using PipeSystem;
using RimWorld;
using TrueMogician.RimWorld.Rimsonable.Patches;
using TrueMogician.RimWorld.Rimsonable.Static;
using Verse;
using VNPE;

namespace TrueMogician.RimWorld.Rimsonable.VNPE;

[StaticConstructorOnStartup]
public static class Initializer {
	static Initializer() {
		IngredientAwareNutrientPastePolicies.AddIngredientProvider(RegisterPipeNetIngredients);
		Helper.Logger.Message("Vanilla Nutrient Paste Expanded support initialized");
	}

	private static bool RegisterPipeNetIngredients(Building_NutrientPasteDispenser dispenser, CompIngredients compIngredients) {
		var resourceComp = GetResourceComp(dispenser);
		if (resourceComp?.PipeNet is not { } pipeNet || pipeNet.Stored < 1)
			return false;

		foreach (var storage in pipeNet.storages) {
			if (storage.parent.TryGetComp<CompRegisterIngredients>() is not { } sourceIngredients)
				continue;
			foreach (var ingredient in sourceIngredients.ingredients)
				compIngredients.RegisterIngredient(ingredient);
		}
		return true;
	}

	private static CompResource? GetResourceComp(Building_NutrientPasteDispenser dispenser) =>
		dispenser is Building_NutrientPasteTap { resourceComp: { } resourceComp }
			? resourceComp
			: dispenser.TryGetComp<CompResource>();
}