using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using TrueMogician.RimWorld.Rimfined.Components;
using TrueMogician.RimWorld.Utility;
using Verse;
using Verse.AI;

namespace TrueMogician.RimWorld.Rimfined.Contents.WorkGiver;

public sealed class CaptureMarkedPawn : WorkGiver_Scanner {
	private readonly Dictionary<(Pawn, Thing, bool), (Job Job, int Tick)> _jobCache = new();

	public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

	public override PathEndMode PathEndMode => PathEndMode.Touch;

	public override bool ShouldSkip(Pawn pawn, bool forced = false) => GetComp(pawn) is not { AnyMarked: true };

	public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) {
		if (GetComp(pawn) is not { AnyMarked: true } comp)
			yield break;
		foreach (var p in comp.ToArray())
			yield return p;
	}

	public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) => TryCreateJobOnThing(pawn, t, forced, out _);

	public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) {
		TryCreateJobOnThing(pawn, t, forced, out var job);
		return job!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static PawnsToCapture? GetComp(Pawn pawn)
		=> pawn.Map is { } map && CachedMapComponent<PawnsToCapture>.Get(map) is { } comp ? comp : null;

	private bool TryCreateJobOnThing(Pawn pawn, Thing thing, bool forced, out Job? job) {
		int tick = Find.TickManager.TicksGame;
		var key = (pawn, thing, forced);
		if (_jobCache.TryGetValue(key, out var tuple) && tuple.Tick == tick) {
			job = tuple.Job;
			return true;
		}
		job = null;
		if (thing is not Pawn target || target.IsForbidden(pawn))
			return false;
		if (GetComp(pawn) is not { } comp || !comp[target])
			return false;
		if (!PawnsToCapture.ValidForCapture(target))
			return false;
		// Vanilla capture uses the same gate.
		if (!HealthAIUtility.CanRescueNow(pawn, target, true))
			return false;
		if (!pawn.CanReserveAndReach(target, PathEndMode, pawn.NormalMaxDanger(), ignoreOtherReservations: forced))
			return false;
		var bed = RestUtility.FindBedFor(target, pawn, false, guestStatus: GuestStatus.Prisoner);
		if (bed is null) {
			JobFailReason.Is("NoPrisonerBed".Translate());
			return false;
		}
		job = JobMaker.MakeJob(JobDefOf.Capture, target, bed);
		job.count = 1;
		_jobCache[key] = (job, tick);
		return true;
	}
}