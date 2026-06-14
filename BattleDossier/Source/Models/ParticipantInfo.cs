using System;
using Verse;

namespace TrueMogician.RimWorld.BattleDossier.Models;

/// <summary>
///     Metadata for one battle participant, addressed by its kind def + load ID. Carries no battle stats —
///     damage, casualties and fate are all derived from the log — only identity resolved on demand
///     against live maps, so a finalized record holds no <see cref="Thing" /> references.
/// </summary>
public record ParticipantInfo : IExposable {
	public string Label = "";
	public string FactionName = "";
	public BattleSide Side;

	private PawnKindDef? _kindDef; // pawns
	private ThingDef? _def;        // buildings
	private int _thingId = -1;

	public ParticipantInfo() { }

	public ParticipantInfo(Thing thing, BattleSide side) {
		_thingId = thing.thingIDNumber;
		Side = side;
		Label = thing.LabelShortCap;
		FactionName = thing.Faction?.Name ?? "";
		if (thing is Pawn pawn)
			_kindDef = pawn.kindDef;
		else
			_def = thing.def;
	}

	public void ExposeData() {
		Scribe_Defs.Look(ref _kindDef, "kindDef");
		Scribe_Defs.Look(ref _def, "def");
		Scribe_Values.Look(ref _thingId, "thingId", -1);
		Scribe_Values.Look(ref Label, "label", "");
		Scribe_Values.Look(ref FactionName, "factionName", "");
		Scribe_Values.Look(ref Side, "side");
	}

	/// <summary>The kind's display name ("Colonist", "Uranium slug turret"), derived from the scribed def.</summary>
	public string KindLabel => _kindDef?.LabelCap ?? (_def?.LabelCap ?? "");

	public float CombatPower => _kindDef?.combatPower ?? 0f;

	public int ThingId => _thingId;

	/// <summary>Computed from the stored def, so the persisted form survives changes to <see cref="ParticipantType" />.</summary>
	public ParticipantType Kind {
		get {
			if (IsBuilding)
				return ParticipantType.Building;
			if (ResolveDef?.race is not { } race)
				return ParticipantType.Other;
			if (race.Humanlike)
				return ParticipantType.Humanlike;
			if (race.IsMechanoid)
				return ParticipantType.Mechanoid;
			return race.Animal ? ParticipantType.Animal : ParticipantType.Other;
		}
	}

	public bool IsBuilding => _def is { category: ThingCategory.Building };

	public bool IsPlayerSide => Side is BattleSide.Colony or BattleSide.Ally;

	/// <summary>The live participant (spawned, not dead), resolved by ID and cached while valid.</summary>
	public Thing? LiveThing {
		get {
			if (field is { Spawned: true } and not Pawn { Dead: true })
				return field;
			return field = ResolveSpawned(ResolveDef, t => t.thingIDNumber == _thingId && t is not Pawn { Dead: true });
		}
	}

	/// <summary>The corpse of a died participant, while it still lies spawned on a map.</summary>
	public Corpse? LiveCorpse {
		get {
			if (field is { Spawned: true })
				return field;
			return field = ResolveSpawned(
				ResolveDef?.race?.corpseDef,
				t => t is Corpse { InnerPawn: { } p } && p.thingIDNumber == _thingId
			) as Corpse;
		}
	}

	// The thing's own def (race for pawns), used to resolve the live instance by ID.
	private ThingDef? ResolveDef => _kindDef?.race ?? _def;

	// Per-def lists are dictionary-indexed by ListerThings, so this scans only things of the same def.
	private static Thing? ResolveSpawned(ThingDef? def, Func<Thing, bool> match) {
		if (def == null)
			return null;
		foreach (var map in Find.Maps) {
			foreach (var thing in map.listerThings.ThingsOfDef(def)) {
				if (match(thing))
					return thing;
			}
		}
		return null;
	}
}