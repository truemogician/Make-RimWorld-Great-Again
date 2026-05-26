# Rimsonable

**A growing collection of "this should be more reasonable" fixes for RimWorld 1.6.**

RimWorld is brilliant at generating stories and alarmingly relaxed about whether anyone in those stories is behaving like a functional adult. Colonists, visitors, and assorted AI-controlled life forms are all perfectly capable of making decisions that would embarrass a medieval peasant. Rimsonable is my long-term collection of focused tweaks for those moments.

This is not a total overhaul. It is not a broad balance pass. It is a toolbox of individually toggleable corrections for mechanics that feel too gamey, too goofy, or too willing to produce nonsense just because the simulation knows you will put up with it.

As the mod grows, the rule stays simple: if vanilla behavior makes me lean back in my chair and ask, "Why are we like this?", it is probably a candidate for Rimsonable.

If you have an idea for another piece of vanilla nonsense that deserves correction, throw it into the Workshop or GitHub discussion. Bad decisions are a renewable resource on the Rim, and I am always accepting new samples.

GitHub repository: https://github.com/truemogician/Make-RimWorld-Great-Again/tree/main/Rimsonable

## For Players

### Design Philosophy

- Small, targeted changes instead of sweeping rewrites. I am here to remove stupidity, not rebuild the planet.
- More internal logic, less "video game because video game."
- Sometimes harsher, usually saner. Common sense is not always comfortable.
- Every feature can be toggled individually in mod settings, and they are true hot toggles: flip them mid-save and the change takes effect immediately, with no need to reload the save or restart the game.
- I'm an experienced engineer obsessed with code quality and performance. Trust my expertise, and my mod will respect your CPU.

Rimsonable is for players who want their colony to act like stressed survivors, not a synchronized troupe of sleep-deprived interns with a death wish.

### Current Features

#### Auto Avoid Proximity Activators

A large mech cluster arrived dormant. You instructed your colonists to forge weapons, build defenses, and fully commit to a prepared battle. And then, all of a sudden, some lovely visitor, despite carrying nothing more threatening than a wine bottle, decided to take a closer look at those sleeping machines. Boom. The battle was on, with your favorite colonist still building a wall next to a mech turret without a weapon in hand.

Looks familiar? Scenes like this are happening all over the Rim, right now! YOU COULD BE NEXT! That is, unless you make the most important decision of your life: prove to yourself that you have the strength and the courage to be **Rimsonable**!

Pawns now stop wandering through dormant mech triggers. Accidentally waking ancient murder machines is now less of a pathfinding hobby.

#### Safe Rest Location

Unless you enjoy your pets becoming some sort of necrophiles, or cosplaying thermoelectric generators by sleeping in the doorway of your freezer, you probably want this.

Animals are not geniuses, but they should at least have the survival instinct of not sleeping in hazardous locations. Rimsonable fixes that. And if your colony is too poor to provide beds for your colonists, they will develop the same common sense.

#### Allow Grenades Through Shields

Shields not caring which side a bullet came from is defensible. But wait, what do you mean a hand-thrown grenade is also a bullet?

If you have ever wanted a melee pawn to sneak to a mech cluster and plant explosives like they mean it, this lets you do that. A pawn standing inside a shield can now throw grenades outward without the shield nobly protecting the enemy from your genius plan.

#### Build at Corners

Place a blueprint in the middle of a `+` of walls and watch vanilla do its favorite trick: colonists cheerfully haul every last resource to the tile, then stand around forever because apparently reaching diagonally through a gap is beyond the physical capabilities of every human and mech on the Rim. The materials sit there, the blueprint sits there, and your careful little wall design stays one tile short of finished.

Rimsonable lets pawns finish those corner blueprints from a diagonal cell. You no longer have to tear down a wall just finish the blueprint, and your colonists can finally get over their fear of diagonal movement.

#### Emergency Jobs Override Schedule

Ever wondered how your colony doctor, despite having a Kindhearted trait, could sleep soundly while the fighter who just protected the colony from a mech cluster is bleeding out next door? Or how your Coward worker could calmly keep watching TV after lighting has just set the whole rec room on fire?

This feature finally teaches your colonists what "emergency" actually means. Schedules no longer prevent your doc from saving lives. Emergency jobs go to whoever is closest, regardless of what their schedule says they should be doing. And if you want to take it further, there is a separate settings toggle (default off) that also pulls colonists off their current work job, not just off recreation or sleep.

Note: emergency jobs are those flagged *emergency* in their Def — you can spot them by the **[E]** suffix in the work priorities panel. Vanilla flags three: firefighting, self-bedding for emergency treatment, and tending patients with urgent needs. Any modded WorkGivers that mark themselves the same way are picked up automatically.

If you are sitting there going *"wait, vanilla already has a flag for this?"* — bingo, you nailed it! RimWorld does have an emergency-job system. But somehow Tynan decided that a regular sleeping habit is still more important than saving your comrade's life. Who knows?

#### Combat Extended: Artillery Marker Enhancement

In CE, the pawn with binoculars finally graduates from battlefield decoration. Artillery now prefers targets with artillery markers, and other turrets become a bit more accurate against them. Point at something, yell "shoot there," and it finally has actual tactical value.

There is also a separate settings toggle for this, disabled by default. Turn it on and artillery will prefer artillery markers even on non-hostile targets, which means it will start firing automatically when a spotter marks a neutral animal or a visitor.

### Compatibility

**Required:** Harmony

**Supported RimWorld version:** 1.6

**Optional integration:**
- Combat Extended
- Vanilla Expanded Framework

Compatibility modules load automatically when the relevant mods are active.

## For Developers

This repository is a monorepo, so shared structure and conventions belong in the root README. The code here is readable enough that it does not need a README-guided walking tour.

For this mod specifically:

- Feature flags and settings wiring live in [`Source/Settings.cs`](Source/Settings.cs)
- Core behavior patches live in [`Source/Patches/`](Source/Patches)
- Stateful systems live in [`Source/Components/`](Source/Components)
- Optional compatibility code lives in [`Source.CE/`](Source.CE) and [`Source.VEF/`](Source.VEF)
- Settings translations live in [`Languages/English/Settings.xml`](Languages/English/Settings.xml)

Build commands:

```bash
dotnet build Source/Rimsonable.csproj
dotnet build Source.CE/Rimsonable.CE.csproj
dotnet build Source.VEF/Rimsonable.VEF.csproj
```

Mod metadata:

- Package ID: `TrueMogician.Rimsonable`
- Author: `true_mogician`

## Closing Thought

RimWorld will probably always be a masterpiece powered in part by extremely questionable decisions. Rimsonable is here to make sure fewer of those decisions are being made by your colonists, your visitors, and whichever idiot thought a mech proximity activator looked interesting.
