using RimWorld;
using TrueMogician.RimWorld.FlippedBuildings.Core;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.FlippedBuildings.Graphics;

// Renders the source mirrored: draws the mirrored rotation's material (E/W swapped) with the mesh UV flipped.
public class Graphic_FlippedMulti : Graphic_Multi {
	// Base returns a plain Graphic_Multi here, which would drop the flip for any recolored/stuffed building.
	public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo) =>
		GraphicDatabase.Get<Graphic_FlippedMulti>(path, newShader, drawSize, newColor, newColorTwo, data, maskPath);

	public override Material MatAt(Rot4 rot, Thing? thing = null) {
		return MirrorTransform.MirrorRotation(rot).AsInt switch {
			0 => MatNorth,
			1 => MatEast,
			2 => MatSouth,
			3 => MatWest,
			_ => BaseContent.BadMat
		};
	}

	public override Mesh MeshAt(Rot4 rot) {
		var size = drawSize;
		if (rot.IsHorizontal && !ShouldDrawRotated)
			size = size.Rotated();
		return FlipUvFor(rot) ? MeshPool.GridPlaneFlip(size) : MeshPool.GridPlane(size);
	}

	public override void Print(SectionLayer layer, Thing thing, float extraRotation) {
		Vector2 size;
		bool flipUv;
		if (ShouldDrawRotated) {
			size = drawSize;
			flipUv = false;
		}
		else {
			size = thing.Rotation.IsHorizontal ? drawSize.Rotated() : drawSize;
			flipUv = FlipUvFor(thing.Rotation);
		}
		if (thing.MultipleItemsPerCellDrawn())
			size *= 0.8f;
		float rotation = AngleFromRot(thing.Rotation) + extraRotation;
		if (flipUv && data != null)
			rotation += data.flipExtraRotation;
		var center = thing.TrueCenter() + DrawOffset(thing.Rotation);
		var material = MatAt(thing.Rotation, thing);
		TryGetTextureAtlasReplacementInfo(material, thing.def.category.ToAtlasGroup(), flipUv, true, out material, out var uvs, out var vertexColor);
		Printer_Plane.PrintPlane(layer, center, size, material, rotation, flipUv, uvs, [vertexColor, vertexColor, vertexColor, vertexColor]);
		ShadowGraphic?.Print(layer, thing, 0f);
	}

	// Always mirror, so negate whatever flip the source already applies at the mirrored rotation (flip-of-flip = unflipped).
	private bool FlipUvFor(Rot4 rot) {
		var mirrored = MirrorTransform.MirrorRotation(rot);
		bool sourceFlip = (mirrored == Rot4.West && WestFlipped) || (mirrored == Rot4.East && EastFlipped);
		return !sourceFlip;
	}
}