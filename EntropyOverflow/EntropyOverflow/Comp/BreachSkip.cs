using System.Linq;
using RimWorld;
using Verse;
using Verse.Sound;

namespace TrueMogician.RimWorld.EntropyOverflow.Comp;

public class BreachSkipProperties : CompProperties_AbilityEffect {
	public float minRadius = 1.0f;

	public float baseRadius = 2.0f;

	public float sensitivityFactor = 1.0f;

	public IntRange stunTicks = new(240, 360);

	public BreachSkipProperties() => compClass = typeof(BreachSkipEffect);

	public float GetEffectiveRadius(Pawn p) {
		var offset = (p.GetStatValue(StatDefOf.PsychicSensitivity) - 1) * sensitivityFactor;
		return minRadius + baseRadius * (1 + offset);
	}
}

// 2. The Logic Class (Execution)
public class BreachSkipEffect : CompAbilityEffect {
	public new BreachSkipProperties Props => (BreachSkipProperties)props;

	public float EffectiveRadius => Props.GetEffectiveRadius(parent.pawn);

	public override void Apply(LocalTargetInfo target, LocalTargetInfo dest) {
		var pawn = parent.pawn;
		var map = pawn.Map;
		var centerCell = target.Cell;

		if (!centerCell.InBounds(map))
			return;

		// Destroy structures
		float radius = EffectiveRadius;
		var cells = GenRadial.RadialCellsAround(centerCell, radius, true).ToList();
		foreach (var cell in cells.Where(c => c.InBounds(map))) {
			// Destroy obstacles (Buildings and Plants)
			// We use ToList() to safely modify the collection while iterating
			var buildings = cell.GetThingList(map)
				.Where(t => t.def.category is ThingCategory.Building)
				.ToList();
			foreach (var b in buildings)
				b.Destroy();
		}

		// Visual explosion effect (no damage)
		MoteMaker.MakeStaticMote(centerCell, map, ThingDefOf.Mote_Bombardment, radius * 2f);

		// Determine valid landing spot: Random cell within the cleared radius
		var landingCell = cells.Where(c => c.Walkable(map))
			.TryRandomElement(out var randomCell)  ? randomCell : centerCell;

		// Execute Teleport
		if (landingCell.IsValid && landingCell.InBounds(map)) {
			pawn.Position = landingCell;
			pawn.Notify_Teleported();
			if (landingCell.Fogged(map))
				FloodFillerFog.FloodUnfog(landingCell, map);
			if (Props.stunTicks.max > 0)
				pawn.stances.stunner.StunFor(Props.stunTicks.RandomInRange, pawn, false);
			SoundDefOf.Psycast_Skip_Exit.PlayOneShot(new TargetInfo(landingCell, map));
			FleckMaker.ThrowLightningGlow(landingCell.ToVector3Shifted(), map, radius);
		}
	}

	// Draw the explosion radius when the player is aiming the ability
	public override void DrawEffectPreview(LocalTargetInfo target) {
		GenDraw.DrawRadiusRing(target.Cell, EffectiveRadius);
	}
}