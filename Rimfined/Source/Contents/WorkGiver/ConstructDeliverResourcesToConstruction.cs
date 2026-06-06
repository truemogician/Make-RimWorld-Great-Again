using System.Collections.Generic;
using RimWorld;
using TrueMogician.RimWorld.Rimfined.Components;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.Rimfined.Contents.WorkGiver;

public sealed class ConstructDeliverResourcesToConstruction : WorkGiver_ConstructDeliverResources {
	public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Construction);

	public override bool Prioritized => true;

	public override bool ShouldSkip(Pawn pawn, bool forced = false) {
		if (!ConstructionPriorityUtility.UseUnifiedConstructionDelivery || pawn.Map is not { } map)
			return true;
		return map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame).Count == 0
			&& map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).Count == 0;
	}

	public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) {
		foreach (var thing in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
			yield return thing;
		foreach (var thing in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
			yield return thing;
	}

	public override float GetPriority(Pawn pawn, TargetInfo t)
		=> t.Thing is null ? 0f : (float)ConstructionPriorityUtility.GetPriority(t.Thing);

	public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) {
		var job = t switch {
			Frame frame         => JobOnFrame(pawn, frame, forced),
			Blueprint blueprint => JobOnBlueprint(pawn, blueprint, forced),
			_                   => null
		};
		PrioritizePrimaryDeliveryTarget(job);
		return job!;
	}

	private static Job? NoCostFrameMakeJobFor(IConstructible c) {
		if (c is Blueprint_Install)
			return null;
		if (c is not Blueprint || c.TotalMaterialCost().Count != 0)
			return null;
		var job = JobMaker.MakeJob(JobDefOf.PlaceNoCostFrame);
		job.targetA = (Thing)c;
		return job;
	}

	private static void PrioritizePrimaryDeliveryTarget(Job? job) {
		if (job?.def != JobDefOf.HaulToContainer || job.targetC.Thing is not { } primary || job.targetB.Thing is not { } dest)
			return;
		if (ConstructionPriorityUtility.GetPriority(primary) <= ConstructionPriorityUtility.GetPriority(dest))
			return;
		job.targetQueueB ??= [];
		job.targetQueueB.RemoveAll(target => target.Thing == primary);
		if (dest != primary)
			job.targetQueueB.Insert(0, dest);
		job.targetB = primary;
	}

	private Job? JobOnFrame(Pawn pawn, Frame frame, bool forced) {
		if (frame.Faction != pawn.Faction)
			return null;
		if (!GenConstruct.CanTouchTargetFromValidCell(frame, pawn))
			return null;
		if (GenConstruct.FirstBlockingThing(frame, pawn) != null)
			return GenConstruct.HandleBlockingThingJob(frame, pawn, forced);
		if (!GenConstruct.CanConstruct(frame, pawn, def.workType, forced, JobDefOf.HaulToContainer))
			return null;
		return ResourceDeliverJobFor(pawn, frame, true, forced);
	}

	private Job? JobOnBlueprint(Pawn pawn, Blueprint blueprint, bool forced) {
		if (blueprint.Faction != pawn.Faction)
			return null;
		if (blueprint.def.entityDefToBuild is ThingDef { plant: not null })
			return null;
		if (!GenConstruct.CanTouchTargetFromValidCell(blueprint, pawn))
			return null;
		if (GenConstruct.FirstBlockingThing(blueprint, pawn) != null)
			return GenConstruct.HandleBlockingThingJob(blueprint, pawn, forced);
		if (!GenConstruct.CanConstruct(blueprint, pawn, def.workType, forced, JobDefOf.HaulToContainer))
			return null;
		if (def.workType != WorkTypeDefOf.Construction && ShouldRemoveExistingFloorFirst(pawn, blueprint))
			return null;
		var job = RemoveExistingFloorJob(pawn, blueprint);
		if (job != null)
			return job;
		job = ResourceDeliverJobFor(pawn, blueprint, true, forced);
		if (job != null)
			return job;
		return def.workType == WorkTypeDefOf.Hauling ? null : NoCostFrameMakeJobFor(blueprint);
	}
}