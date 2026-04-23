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

#### Work Memory

Quality crafting ramps up when a pawn stays focused on the same recipe. Cold starts are real: a fresh attempt begins at 50% speed, and steady repetition can climb to 125%. Turns out practice helps, repetition builds rhythm, and abandoning the bench for a week is not actually a productivity strategy. Who knew?

In practical terms, a 400-work recipe hits the midpoint after about 400 uninterrupted work ticks and tops out after about 800. A 1000-work recipe takes about 1000 and 2000. Work memory also gets an 800-tick grace period before it starts fading, so a short interruption will not immediately turn your crafter back into an amnesiac.

There is also an optional sub-setting for players who want the same mechanic on bills that do not produce quality items, such as cooking or refining.

And if you use RimHUD, the current work memory bonus now shows up on RimHUD's Activity row instead of vanishing just because the vanilla pawn card got replaced. Common sense extends to UI compatibility as well.

#### Allow Grenades Through Shields

Shields not caring which side a bullet came from is defensible. But wait, what do you mean a hand-thrown grenade is also a bullet?

If you have ever wanted a melee pawn to sneak to a mech cluster and plant explosives like they mean it, this lets you do that. A pawn standing inside a shield can now throw grenades outward without the shield nobly protecting the enemy from your genius plan.

#### Combat Extended: Artillery Marker Enhancement

In CE, the pawn with binoculars finally graduates from battlefield decoration. Artillery now prefers targets with artillery markers, and other turrets become a bit more accurate against them. Point at something, yell "shoot there," and it finally has actual tactical value.

There is also a separate settings toggle for this, disabled by default. Turn it on and artillery will prefer artillery markers even on non-hostile targets, which means it will start firing automatically when a spotter marks a neutral animal or a visitor.

### Compatibility

- **Required:** Harmony
- **Supported RimWorld version:** 1.6
- **Optional integration:** Combat Extended
- **Optional integration:** RimHUD
- **Optional integration:** Vanilla Expanded Framework

Compatibility modules load automatically when the relevant mods are active.

## For Developers

This repository is a monorepo, so shared structure and conventions belong in the root README. The code here is readable enough that it does not need a README-guided walking tour.

For this mod specifically:

- Feature flags and settings wiring live in [`Source/Settings.cs`](Source/Settings.cs)
- Core behavior patches live in [`Source/Patches/`](Source/Patches)
- Stateful systems live in [`Source/Components/`](Source/Components)
- Optional compatibility code lives in [`Source.CE/`](Source.CE), [`Source.RimHUD/`](Source.RimHUD), and [`Source.VEF/`](Source.VEF)
- Settings translations live in [`Languages/English/Settings.xml`](Languages/English/Settings.xml)

Build commands:

```bash
dotnet build Source/Rimsonable.csproj
dotnet build Source.CE/Rimsonable.CE.csproj
dotnet build Source.RimHUD/Rimsonable.RimHUD.csproj
dotnet build Source.VEF/Rimsonable.VEF.csproj
```

Mod metadata:

- Package ID: `TrueMogician.Rimsonable`
- Author: `true_mogician`

## Closing Thought

RimWorld will probably always be a masterpiece powered in part by extremely questionable decisions. Rimsonable is here to make sure fewer of those decisions are being made by your colonists, your visitors, and whichever idiot thought a mech proximity activator looked interesting.
