using RimWorld;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.FlippedBuildings.Graphics;

// Mirrors a single texture via UV flip. When drawn rotated, the angle is also negated, since reflecting a
// rotated sprite reverses its rotation: Mirror(Rotate(T, θ)) = Rotate(FlipH(T), -θ).
public class Graphic_FlippedSingle : Graphic_Single {
	public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo) =>
		GraphicDatabase.Get<Graphic_FlippedSingle>(path, newShader, drawSize, newColor, newColorTwo, data);

	public override void Print(SectionLayer layer, Thing thing, float extraRotation) {
		var size = thing.Rotation.IsHorizontal && !ShouldDrawRotated ? drawSize.Rotated() : drawSize;
		if (thing.MultipleItemsPerCellDrawn())
			size *= 0.8f;
		float angle = -AngleFromRot(thing.Rotation) + extraRotation;
		if (data != null)
			angle += data.flipExtraRotation;
		var center = thing.TrueCenter() + DrawOffset(thing.Rotation);
		var material = MatAt(thing.Rotation, thing);
		TryGetTextureAtlasReplacementInfo(material, thing.def.category.ToAtlasGroup(), true, true, out material, out var uvs, out var vertexColor);
		Printer_Plane.PrintPlane(layer, center, size, material, angle, true, uvs, [vertexColor, vertexColor, vertexColor, vertexColor]);
		ShadowGraphic?.Print(layer, thing, 0f);
	}
}