using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using TrueMogician.RimWorld.FlippedBuildings.Defs;
using TrueMogician.RimWorld.FlippedBuildings.Graphics;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.FlippedBuildings.Core;

// Twins are registered before vanilla generates blueprint/frame defs and before reference resolution and
// short hashing, so those passes treat them as first-class defs. See GenerateImpliedDefsPatch for the hook.
public static class FlipDefGenerator {
	private const string _DEF_NAME_PREFIX = "Flipped_";

	private static readonly Dictionary<Type, FieldInfo[]> _graphicDataFields = new();

	public static void GenerateAll() {
		FlipRegistry.Clear();
		var settings = Mod.Settings;
		var generated = 0;
		// Generate for every detected building; the per-building setting is a live UI gate, not a generation gate.
		bool masterEnabled = settings is not { MasterEnabled: false };
		// AddImpliedDef mutates the database, so iterate a snapshot.
		foreach (var source in DefDatabase<ThingDef>.AllDefs.ToList()) {
			if (!FlipEligibility.IsEligible(source))
				continue;
			FlipRegistry.RecordCandidate(source);
			if (!masterEnabled)
				continue;
			var flipped = CreateFlippedDef(source);
			DefGenerator.AddImpliedDef(flipped);
			FlipRegistry.Register(source, flipped);
			AugmentIdentityLists(source, flipped);
			generated++;
		}
		Helper.Logger.Message($"Detected {FlipRegistry.Candidates.Count} flippable building(s); generated {generated} flipped twin(s).");
	}

	private static ThingDef CreateFlippedDef(ThingDef source) {
		var flipped = Gen.MemberwiseClone(source);
		flipped.defName = _DEF_NAME_PREFIX + source.defName;
		flipped.shortHash = 0;
		flipped.label = "FlippedBuildings.FlippedLabel".Translate(source.label);
		flipped.ClearCachedData();

		// Hidden from architect/research/filter UIs; designationCategory stays so blueprint/frame defs still generate.
		flipped.canGenerateDefaultDesignator = false;
		flipped.designatorDropdown = null;
		flipped.tradeability = Tradeability.Sellable;
		flipped.thingSetMakerTags = null;

		// Own list (not the shared clone); link to source for vanilla's filter cascade and filter-tree hiding.
		flipped.virtualDefs = [];
		flipped.virtualDefParent = source;
		source.virtualDefs.Add(flipped);

		MirrorGeometry(source, flipped);
		return flipped;
	}

	private static void MirrorGeometry(ThingDef source, ThingDef flipped) {
		var size = source.size;
		if (flipped.hasInteractionCell)
			flipped.interactionCellOffset = MirrorTransform.MirrorCellOffset(source.interactionCellOffset, size);
		if (!source.multipleInteractionCellOffsets.NullOrEmpty()) {
			flipped.multipleInteractionCellOffsets =
				source.multipleInteractionCellOffsets.Select(o => MirrorTransform.MirrorCellOffset(o, size)).ToList();
		}
		if (source.graphicData != null)
			flipped.graphicData = MirrorGraphicData(source.graphicData, source.GetModExtension<FlipSpec>());

		MirrorComps(source, flipped);
	}

	private static GraphicData MirrorGraphicData(GraphicData src, FlipSpec? spec) {
		var data = new GraphicData();
		data.CopyFrom(src);
		data.shaderParameters = src.shaderParameters;
		data.drawOffset = MirrorTransform.MirrorDrawOffset(src.drawOffset);
		data.drawOffsetNorth = MirrorNullable(src.drawOffsetNorth);
		data.drawOffsetSouth = MirrorNullable(src.drawOffsetSouth);
		// East and West swap, then mirror, so a side-specific offset follows its texture.
		data.drawOffsetEast = MirrorNullable(src.drawOffsetWest);
		data.drawOffsetWest = MirrorNullable(src.drawOffsetEast);
		if (src.shadowData != null)
			data.shadowData = new ShadowData { volume = src.shadowData.volume, offset = MirrorTransform.MirrorDrawOffset(src.shadowData.offset) };

		if (spec?.mirroredTexturePath is { } texPath)
			data.texPath = texPath; // pre-made mirrored art; keep the source graphic class
		else if (src.graphicClass == typeof(Graphic_Multi))
			data.graphicClass = typeof(Graphic_FlippedMulti);
		else if (src.graphicClass == typeof(Graphic_Single))
			data.graphicClass = typeof(Graphic_FlippedSingle);
		// Other graphic classes keep their type: gameplay still mirrors, visuals fall back to source.
		return data;

		static Vector3? MirrorNullable(Vector3? v) => v.HasValue ? MirrorTransform.MirrorDrawOffset(v.Value) : null;
	}

	// Clones (comps are shared by MemberwiseClone) and mirrors comp properties needing per-twin state:
	// GraphicData overlays (found by reflection, e.g. CompEmptyStateGraphic's open-door, so modded overlays
	// work too), registered offset mirrorers, and CompProperties_Facility (resolution writes parent-specific
	// links that would otherwise clobber the source's).
	private static void MirrorComps(ThingDef source, ThingDef flipped) {
		if (source.comps is not { Count: > 0 })
			return;
		var context = new MirrorContext(source.size);
		for (var i = 0; i < source.comps.Count; i++) {
			var comp = source.comps[i];
			var graphicFields = GraphicDataFields(comp.GetType());
			if (graphicFields.Length == 0 && !MirrorerRegistry.HasMirrorer(comp.GetType()) && comp is not CompProperties_Facility)
				continue;
			if (ReferenceEquals(flipped.comps, source.comps))
				flipped.comps = [..source.comps];
			var clone = Gen.MemberwiseClone(comp);
			foreach (var field in graphicFields) {
				if (field.GetValue(comp) is GraphicData graphicData)
					field.SetValue(clone, MirrorGraphicData(graphicData, null));
			}
			MirrorerRegistry.ApplyTo(clone, context);
			flipped.comps[i] = clone;
		}
	}

	private static FieldInfo[] GraphicDataFields(Type compPropsType) {
		if (!_graphicDataFields.TryGetValue(compPropsType, out var fields)) {
			fields = compPropsType.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => f.FieldType == typeof(GraphicData)).ToArray();
			_graphicDataFields[compPropsType] = fields;
		}
		return fields;
	}

	// Appends the twin to def-reference lists (recipe users, facility links) so list-driven systems treat it
	// like the canonical without a runtime patch. Runs pre-resolve, before those caches are built.
	private static void AugmentIdentityLists(ThingDef source, ThingDef flipped) {
		foreach (var recipe in DefDatabase<RecipeDef>.AllDefsListForReading) {
			if (recipe.recipeUsers != null && recipe.recipeUsers.Contains(source))
				recipe.recipeUsers.Add(flipped);
		}

		foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading) {
			var affected = def.GetCompProperties<CompProperties_AffectedByFacilities>();
			if (affected?.linkableFacilities != null && affected.linkableFacilities.Contains(source))
				affected.linkableFacilities.Add(flipped);
		}
	}
}