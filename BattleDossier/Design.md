# Battle Dossier — Design

Each battle produces a persistent **dossier**: per-participant combat performance for *all* combatants — colonists, allies, enemies, mechs, animals, **and combat buildings** (turrets, traps) — plus a timeline of notable events, casualty summaries per side, and a leaderboard view with faction filtering. Sessions start and end automatically; concurrent engagements merge heuristically. Dossiers are saved in the game save indefinitely (with player-controlled deletion and an optional rolling window) — every past battle can be reviewed at any time. Targets RimWorld 1.6.

Scoring and social/mood rewards are deliberately deferred to a later phase (§8): v1 is the dossier system itself, built and tested first.

## 1. Problem & Player Value

Battles are the emotional peaks of a colony's story, but the game gives no per-battle accounting of who actually carried the fight, what it cost, or who fell. Vanilla per-pawn records (`Kills`, `DamageDealt`, …) are lifetime totals; the battle log records *events* but no numbers, and trims itself after ~7 in-game days (`BattleLog.ReduceToCapacity`, `Verse/BattleLog.cs:82`). Players who want to know "who won us that raid, and what did it cost" have nothing — and a year later, the raid where their best melee blocker died is gone from every in-game record.

The mod turns each battle into a permanently archived story beat: a post-battle dossier with leaderboard, casualties, and timeline, plus a browsable battle history.

Workshop precedent is thin: the closest mod, [DamageStatistics](https://steamcommunity.com/sharedfiles/filedetails/?id=3442531626), is a live damage meter that only counts while its window is open, persists nothing, and has no per-battle sessions, ranking, or history. The battle-session lifecycle and dossier archive are unoccupied design space.

## 2. Core Decision: Reuse Vanilla Battle Grouping

Vanilla already solves the two hardest lifecycle problems — *what counts as one battle* and *when it ends*:

- Every combat event (melee hit, ranged impact, explosion, downed/killed transition) is pushed to `Find.BattleLog.Add(LogEntry)` (`Verse/BattleLog.cs:23`). The grouping is **pawn-based, not time-window-based**: each entry's concerned pawns are checked for an active battle (`Pawn_RecordsTracker.BattleActive`), the most important one wins, and battles sharing pawns are merged via `Battle.Absorb` (`Verse/Battle.cs:90`). Otherwise a new `Battle` is created.
- "Battle membership expires" is a per-pawn deadline: `EnterBattle` sets `battleExitTick = TicksGame + 5000` (`Pawn_RecordsTracker.cs:74`, constant `Battle.TicksForBattleExit`). A battle is effectively over when no entry has concerned its pawns for 5000 ticks (~2 in-game hours).
- The pawn log ITab's battle headlines are exactly these `Battle` objects (`ITab_Pawn_Log_Utility.GenerateLogLinesFor`), and `Battle` is `IExposable` + `ILoadReferenceable`, so it can be cross-referenced from live mod data.

**Design**: a dossier *session* is keyed to vanilla `Battle` objects. We never decide battle identity ourselves at the entry level — we observe battle creation/merging through one cheap Harmony postfix on `BattleLog.Add`, and inherit vanilla's merge semantics by following the `AbsorbedBy` chain (the same lazy-walk vanilla does in `Pawn_RecordsTracker.BattleActive`). One session can span **multiple** vanilla Battles: concurrent engagements that vanilla keeps separate (no shared pawn) are merged at the session layer by heuristic (§5), without touching vanilla's battle objects or the pawn-log display.

What vanilla does **not** provide, and we must add ourselves:

- **Damage amounts.** No `BattleLogEntry_*` subclass stores damage numbers — only body parts hit and destroyed flags (`DamageWorker.DamageResult.AssociateWithLog`, `Verse/DamageWorker.cs:278`). The authoritative number is `DamageResult.totalDamageDealt`, available only at `Thing.TakeDamage` time (`Verse/Thing.cs:927-939`, where vanilla credits `RecordDefOf.DamageDealt`). Stat collection therefore hooks the damage pipeline, not the log (§4).
- **Non-pawn credit.** Vanilla records only credit pawn instigators (`RecordsUtility.Notify_PawnKilled/Downed` take `Pawn`; `Thing.TakeDamage` only writes `DamageDealt` for pawn instigators). Buildings (turrets, traps) pass themselves as `DamageInfo.Instigator` — traps explicitly (`Building_TrapDamager.cs:31`), unmanned turrets via projectile launcher — so our hooks read the instigator as a generic `Thing` and credit buildings as first-class participants (§4).
- **A battle-end *event*.** Vanilla's exit is implicit (deadline passes; nothing fires). There is no "raid defeated" callback either — fleeing/leaving messages are per-lord `TransitionAction_Message`s (`LordJob_AssaultColony.cs:99`, `Lord.cs:159`), too strategy-specific to hook. We detect the end by polling cheap vanilla state (§5).
- **Permanence.** Vanilla battles are trimmed after 420000 ticks and entries reference pawns that get discarded (`Battle.Notify_PawnDiscarded`, `Verse/Battle.cs:103`). Dossiers therefore copy everything they need out of vanilla objects at collection/finalization time — text-rendered events, snapshotted identities, summed numbers — and never depend on a `Battle` or `LogEntry` surviving (§6, §9).

### Rejected alternatives

- **Diffing vanilla lifetime records at battle start/end** (snapshot `DamageDealt`, `Kills`, … per pawn, subtract). Zero damage-path patches, but coarse: lifetime `DamageDealt` includes friendly fire, sparring, and damage to neutral things; no hostility filtering, no per-battle kill attribution, no building credit, and concurrent non-battle damage pollutes the diff. Kept as a sanity-check tool in dev mode, not the data source.
- **Parsing battle-log entries to reconstruct stats.** Entries lack amounts entirely; melee misses/dodges and ranged-fire entries would let us count attacks but never damage. Dead end.
- **Own battle detection (DangerWatcher edges / lord lifecycle).** Reimplements what `BattleLog` already does, and diverges from the in-game pawn-log headlines the player can see. Worse fidelity, more code.
- **Patching `Battle.Absorb` to force-merge concurrent battles inside vanilla.** Would change the pawn-log display and vanilla save data, and fight `BattleLog.Add`'s per-entry battle selection. Session-layer merging gets the same player-facing result reversibly.

## 3. Architecture Overview

| Piece | Type | Responsibility |
| ----- | ---- | -------------- |
| `DossierManager` | `GameComponent` | Owns active sessions and finished `BattleDossier`s; applies merge heuristics; polls for battle end (interval tick); scribes everything; enforces the rolling window |
| `BattleSession` | plain class | Live accumulator keyed by a **set** of vanilla `Battle` references; per-participant `ParticipantStats`; growing event timeline; casualty tallies |
| `BattleDossier` | `IExposable`, `ILoadReferenceable` | Finalized, immutable result: battle name, ticks, duration, maps, factions, participant stats for all sides, casualty summary per side, event timeline (plain text), outcome |
| `ParticipantStats` | `IExposable` | **Snapshot-first**: identity captured at row creation (name, kind/def label, faction name, side, isBuilding) plus fate; a `Scribe_References` to the live Thing kept only as an optional convenience (§7) |
| `DossierEvent` | `IExposable` | Tick + category (enum: kill, down, sideJoined, merge, start, end, …) + pre-rendered text + snapshotted actor names |
| Harmony patches | static classes under `Patches/` | Stat collection (§4), session start + timeline + casualties (§5), all on combat-event paths |
| `Letter_BattleEnded` | `ChoiceLetter` subclass | Battle-end notification; opens the dossier window (§7) |
| `Window_BattleDossier` | `Window` subclass | Tabbed dossier view: Overview / Leaderboard / Timeline, plus dossier browser with deletion (§7) |

`GameComponent` (not `MapComponent`): vanilla `Battle` is game-global, battles can span maps (player caravan ambushed while home map fights), and history must survive map loss.

**Snapshot-first is a hard rule**: vanilla aggressively discards dead world pawns, enemy corpses despawn, buildings get destroyed or deconstructed, factions can be removed. Every UI render path works exclusively from snapshotted data; the live reference is never required for anything and is null-checked at its single use site (the "view current info" affordance, §7). This is the `NullReferenceException` firewall.

## 4. Stat Collection

All hooks are event-driven (no per-tick work) and fire only while at least one session is active — first check in each postfix is a cheap `anyActiveSession` flag.

**All combatants are tracked**: any pawn *or building* that deals or receives a credited combat event in a session's battles gets a `ParticipantStats` row, tagged with a *side* (Colony / Ally / Enemy / Wild) derived from faction relation to the player at first participation. Colony side includes slaves, colony mechs, colony animals, and colony buildings; enemy siege mortars and cluster turrets land on the enemy side the same way. Manned turrets resolve to the manning pawn (vanilla does this in `Verb_LaunchProjectile.cs:100-106`), so a building row means the building itself fought (unmanned turret, trap).

| Stat | Hook | Attribution details |
| ---- | ---- | ------------------- |
| Damage dealt / taken | Postfix `Thing.TakeDamage(DamageInfo)` → read returned `DamageResult.totalDamageDealt` (`Verse/Thing.cs:908`) | `dinfo.Instigator` as generic `Thing`: pawns and buildings credited alike (explosions carry `explosion.instigator`, `DamageWorker.cs:212`; traps pass themselves, `Building_TrapDamager.cs:31`). Credit *dealt* when instigator and victim are mutually hostile at hit time; *friendly fire* (victim non-hostile to instigator) is a separate column on every row — enemy-side friendly fire is fun data ("the centipede that mowed down its escorts"). Credit *taken* on the victim's row when `dinfo.Def.ExternalViolenceFor`; buildings taking damage counts too (turret chewed down by sappers) |
| Kills | Postfix `Pawn.Kill` (`Verse/Pawn.cs:966`) reading `dinfo?.Instigator` as `Thing` | Vanilla's own credit path (`RecordsUtility.Notify_PawnKilled`, called at `Pawn.cs:2805`) is pawn-only; patching `Kill` directly captures building killers with one hook. Victim `kindDef.combatPower` snapshotted for later scoring (§8) |
| Downs | Postfix `Pawn_HealthTracker.MakeDowned` (`Pawn_HealthTracker.cs:840`) reading `dinfo?.Instigator` as `Thing` | Same shape as kills (vanilla's `Notify_PawnDowned` at `:881` is pawn-only). **Downs and kills are independent tallies**: a pawn downed by A and later finished by B credits A a down and B a kill; a downed pawn bleeding out later credits nobody for the death — intended, not a gap |
| Casualties & timeline | The existing `BattleLog.Add` postfix (§5): every death/down emits a `BattleLogEntry_StateTransition` there, **including unattributed ones** (bleed-outs, fires) | Increments the per-side casualty tallies and appends a timeline event. Deliberately a different source than the credit hooks: credits need an instigator, casualties must not |
| Shots fired / melee swings | Postfix `Verb_Shoot.TryCastShot` (`Verse/Verb_Shoot.cs:29`, where vanilla increments `ShotsFired`) and `Verb_MeleeAttack.TryCastShot` | Powers an accuracy/effort column; cheap counters. Verb caster covers turrets (caster = building) |
| Participation window | Recorded on first credited event per participant; last-event tick updated per event | Used for "joined late" display |

Remaining unattributed damage (null instigator: fire spread, gas, collapsing roofs) lands in a session-level "unattributed" line in the Overview; the resulting deaths still reach casualties/timeline via state transitions.

Vanilla `DamageDealt`/`DamageTaken` records keep working untouched; we never write vanilla records.

### Attribution resolver pipeline (extensibility)

The generic instigator-based attribution above will be wrong for some modded sources — DoTs applied as hediffs (instigator long gone by the tick that deals damage), summoned entities (credit should flow to the summoner), CE ammo internals, ability projectiles with detached instigators. Instead of hard-coding cases, every credit decision flows through a small resolver pipeline that compat modules extend:

```csharp
public readonly struct AttributionContext {       // what the hook saw
	public Thing Victim { get; init; }
	public DamageInfo? Dinfo { get; init; }        // null for e.g. hediff deaths
	public DamageWorker.DamageResult Result { get; init; }  // null outside TakeDamage
	public AttributionKind Kind { get; init; }     // Damage | Kill | Down
}

public static class AttributionResolver {
	// Highest priority first; first non-null wins. Returns the Thing to credit.
	public static void Register(IAttributionHandler handler, int priority = 0);
}

public interface IAttributionHandler {
	// Return the credited Thing, or null to pass to the next handler.
	Thing Resolve(in AttributionContext context);
}
```

- The **default handler** (priority 0, always last) implements §4's logic: `dinfo.Instigator`, manned-turret manning resolution, explosion instigators. The vanilla path stays a handler like any other — no special cases in the hooks.
- The pipeline is consulted once per credited event (damage application, kill, down) — already heavyweight paths, and handler lists are tiny, so no performance concern.
- A handler returning the victim itself or a non-participant is validated by the caller (hostility gate, side assignment) exactly like the default result — handlers decide *who*, the core decides *whether it counts*.
- Registration is plain C# (static method call from the compat module's `Initializer`, per the repo's `Source.*` + `LoadFolders.xml` convention). No defs or reflection scanning needed for v1; a `DefModExtension`-based declarative layer can come later if XML-only mods want in.
- Ships with the default handler only. The first real consumer is expected to be the CE compat module (`BattleDossier.CE`) once CE's instigator fidelity is verified (§10); summoner-credit handlers for VEF-style abilities are a natural second.

The same interface answers a subtler need: *re-attribution*. A handler may map credit from a non-participant proxy (a spawned `Thing` like a drone or projectile-spawned fire) to its owner, which the default instigator logic can never do.

### Fate classification

At finalization, every pawn participant gets a fate: *died*, *downed* (and survived), *captured* (downed enemy in player custody at end — **Unverified**: cleanest check is `pawn.IsPrisonerOfColony` at finalize time), *fled* (enemy alive, despawned or exiting), *fought on*. Buildings get *intact* or *destroyed*. Fates power the casualty summary: colony losses, ally losses, enemy dead/downed/captured/fled — the after-action numbers players currently tally by hand.

## 5. Session Lifecycle

Fully automatic — no manual start/stop. The dossier browser shows the live session (clearly marked "in progress") so the player can watch mid-battle.

### Auto start

Postfix `BattleLog.Add`: after vanilla files the entry into a `Battle`, check (in order, all cheap):

1. No session already covers that battle (after following `AbsorbedBy` chains through every session's battle set).
2. The entry concerns at least one player-faction pawn.
3. The opposing side is genuinely hostile: entry initiator/recipient `HostileTo(Faction.OfPlayer)`, **or** the map currently reports a threat — `GenHostility.AnyHostileActiveThreatToPlayer(map)` (`RimWorld/GenHostility.cs:55`) or `map.dangerWatcher.DangerRating >= StoryDanger.Low`.

Condition 3 filters hunting, sparring, animal-training nicks, and social fights (vanilla also logs those into Battles). Predator revenge and manhunters pass via `HostileTo`. This covers every trigger in the idea note — raids, mech assaults, dormant-mech wake-ups (waking mechs become active threats; their first shot/hit is a log entry) — without enumerating incident types.

The same postfix is the **timeline/casualty feed** for already-running sessions: state transitions (kills, downs) become `DossierEvent`s rendered to text immediately via `entry.ToGameStringFromPOV(null)` (`Verse/LogEntry.cs:52`) — capturing the vanilla-flavored sentence ("X was killed by Y") while the pawns still exist. Only *notable* entries become timeline events (state transitions, session lifecycle, side joins); per-hit spam stays aggregated in stats, keeping dossier size bounded (§9).

### Merging

Two layers:

1. **Vanilla merges** (shared pawn → `Battle.Absorb`): handled implicitly. Sessions look up battles by walking `AbsorbedBy` chains on every access; if absorption bridges two sessions' battle sets, the sessions merge additively (stats summed per participant, timelines interleaved by tick, casualty tallies summed).
2. **Heuristic merges** (the concurrent-battle problem): vanilla keeps two fronts of one raid as separate Battles when no pawn fights in both. Confirmed by gameplay observation. On session start, `DossierManager` checks every other active session; two sessions merge when they are simultaneously active **and** share at least one involved map. Cross-map sessions stay separate — a caravan ambush during a home-map raid is genuinely a different battle.

   False positives are accepted by design: an unrelated predator attack or a second faction's raid during an ongoing fight merges into the same dossier — to the player this reads as one defense of the colony, and the faction filter in the leaderboard view (§7) splits it visually when wanted. No same-faction or animal-exclusion knobs; fewer settings, simpler invariants.

   Merged sessions record a timeline event ("Second front: {battle name}") and keep the union of battle references; the dossier title uses the largest battle's name plus a front count.

### Auto end

`DossierManager` polls active sessions every 250 ticks (only while sessions exist). A session ends when **both**:

- `TicksGame > max(LastEntryTimestamp over the session's battles) + 5000` — vanilla's own battle-exit window; the pawn-log headlines close on the same boundary, so mod and game agree.
- No hostile active threat remains on the session's involved maps (`GenHostility.AnyHostileActiveThreatToPlayer`). This keeps the session open during lulls while raiders regroup — kill events during a long raid with quiet gaps still land in one dossier — and matches the player-felt "battle over" moment (threat cleared), at the cost of waiting out fleeing stragglers.

Safety valve: if the first condition has held for a long cap (default 15000 ticks ≈ 6 hours) the session ends regardless of lingering threats (e.g., an unreachable turret across the map). Cap configurable.

Outcome classification at end: **victory** (threats cleared, any colonist still free), **defeat** (ended with player pawns all downed/dead/fled), or **expired** (safety-valve end with threats still present). Displayed on the Overview; also gates future rewards (§8).

## 6. Dossier Contents

A finished `BattleDossier` is fully self-contained:

- Header: battle name (vanilla grammar via `Battle.GetName()`, copied at finalize), start/end ticks, duration, map names, involved faction names with relation at the time.
- Participant rows (`ParticipantStats`): snapshot identity + side + fate + stat block (damage dealt, damage taken, friendly fire, kills, downs, shots/swings, participation window) + summed victim `combatPower` (stored now, consumed by scoring later).
- Casualty summary per side: dead / downed / captured / fled (pawns), destroyed (buildings).
- Aggregates: total damage exchanged, unattributed-damage line, enemy total `combatPower`.
- Timeline: capped list of `DossierEvent`s (pre-rendered text).
- Outcome.

## 7. Dossier UX & History

### Battle-end letter

On session end, send a `Letter_BattleEnded : ChoiceLetter` (gold/positive `LetterDef` on victory, neutral otherwise) — label "Battle ended: {battle name}". `Choices` offers **View dossier** (opens `Window_BattleDossier`) and close; `OpenLetter` is overridden so clicking always leads to the dossier rather than a text dialog.

Letters land in `Find.Archive` when dismissed, so the History → Messages tab lists battle letters and re-opening one from the archive calls the same `OpenLetter`. The archive is a *convenience surface* — the canonical history is the mod's own dossier browser, immune to the archive's 200-item cull (`RimWorld/Archive.cs:69`). The letter scribes a `Scribe_References` to its `BattleDossier`; a culled letter loses nothing, and a letter whose dossier was deleted by the player simply opens the browser.

Settings: auto-open the window instead of (or in addition to) the letter; suppress letters for small skirmishes below a configurable threat-points threshold (dossier still recorded — suppression is about noise, not data).

### Dossier window

Custom `Window` (~950×650) with three tabs:

- **Overview** — battle name, date, duration, maps, involved factions with side icons; outcome banner; casualty summary table (per side: dead / downed / captured / fled / buildings destroyed); aggregate lines (total damage exchanged, unattributed damage, enemy total `combatPower`).
- **Leaderboard** — table over all participants: *Pawn/Building (icon + name + faction icon), Side, Damage dealt, Kills, Downs, Damage taken, Friendly fire, Accuracy, Fate*. Default sort: damage dealt descending; sortable by any column. **Faction filter** row (toggle chips: All / Colony / per involved faction) plus a "colony only" quick toggle. Buildings render with their def icon instead of a portrait. Every cell renders from snapshots; when the live Thing reference still resolves (spawned pawn, surviving prisoner, intact building), the row shows a small "view" affordance — click selects-and-jumps (`CameraJumper`) and opens the vanilla info card — strictly additive, absent for dead/discarded participants.
- **Timeline** — chronological `DossierEvent` list with tick→clock rendering, category icons, and the pre-rendered vanilla sentences; filterable by category (kills only, etc.).

Not `PawnTable`/`PawnColumnDef`: that framework assumes live pawns and def-driven columns; a dossier contains mostly-dead pawns, buildings, and frozen numbers. A plain sorted-list table over `ParticipantStats` is less code.

### Dossier browser

The window header hosts a **battle history** list: all stored dossiers, newest first (live session pinned on top, marked in-progress), with search by name/faction, a pin marker, and a **delete button per entry** (confirmation dialog; pinned dossiers require unpinning first). Reachable from the letter window and a keybind / main-tab affordance (**Unverified**: patching `MainTabWindow_History` tab list vs. offering the browser from inside `Window_BattleDossier` only; decide at implementation, the latter is zero-risk).

## 8. Phase 2 (post-v1): Scoring & Rewards

Deferred until the dossier core is built and tested. Design intent recorded here so v1 stores what phase 2 needs (per-victim `combatPower` sums are already collected):

- **Scoring**: one score per participant from the stored stat block — damage dealt + combatPower-weighted kills/downs + small positive weight on damage taken (tanking) − friendly-fire penalty; weights in settings. Computed at display time from stored stats, so old dossiers gain scores retroactively when phase 2 lands and re-rank live when weights change — nothing about scoring needs to be persisted.
- **MVP & podium**: highest-scoring colony pawn badged separately from the global ranking (the global #1 may legitimately be an enemy).
- **Rewards** (victory + minimum enemy `combatPower` scale only): mood memories for MVP/podium/participants, plus the vanilla `DefeatedHostileFactionLeader(/Opinion)` pattern (`Thoughts_Memory_Misc.xml:574-601`) — an expirable `TaleDef` + `Thought_Tale` social ThoughtDef so pawns who know the MVP respect them, fading as the tale expires (`RimWorld/Thought_Tale.cs:12`). Animals/mechs/buildings can top the board but receive no opinion respect (`ThoughtWorker_Tale` requires humanlike subjects).

## 9. Persistence

Dossiers are first-class save data. Retention is player-controlled, not automatic:

- **Manual deletion**: every dossier is deletable from the browser (§7).
- **Rolling window**: settings expose `maxStoredDossiers` (default **0 = unlimited**); when set, finalizing a new dossier drops the oldest unpinned ones beyond the cap. Pinned dossiers are exempt and don't count toward the cap.
- `DossierManager` scribes: finished `BattleDossier` list (Deep), active sessions (Deep — the vanilla `Battle` references scribe as `Scribe_References` since `Battle : ILoadReferenceable`), next dossier ID.
- Finished dossiers are **fully self-contained**: names, faction labels, timeline text, and participant identities are snapshotted at collection/finalization time; no references to `Battle`, `LogEntry`, `Faction`, or `Map` objects survive into a finished dossier (Thing refs kept only as the soft §7 convenience). Vanilla trimming, pawn discarding, and faction removal can't touch history.
- A live session whose `Battle` references are all lost on load (vanilla trimmed them — 420000-tick retention) finalizes immediately with outcome "expired".
- **Size discipline** is what makes "unlimited by default" viable: a dossier stores per-participant numbers (~10 floats/ints + short strings) and a *capped* timeline (notable events only; hard cap ~200 events per dossier, oldest non-kill events dropped first). Ballpark: a 30-participant raid ≈ a few KB of XML — a 100-battle colony adds well under a MB to the save.
- Letters scribe through the vanilla letter/archive pipeline; the dossier reference resolves via `ILoadReferenceable` and tolerates deletion.
- Adding the mod mid-save: fine (empty component state). Removing it: standard unknown-component/letter warnings on next load, no corruption — vanilla discards unknown letter classes with a one-time error. Stated in mod description; no migration shims (repo policy).

## 10. Compatibility & Risks

1. **Combat Extended** — CE replaces projectile/armor internals but final damage still flows through `Thing.TakeDamage` and downs/kills through `Pawn.Kill`/`MakeDowned`. **Unverified**: CE's ammo/fragment instigator fidelity and whether its armor pipeline reports `totalDamageDealt` consistently. Any fixes live in a `BattleDossier.CE` compat module (repo `.CE` convention) registering an attribution handler (§4) — no core changes. Verify early in implementation against `Knowledge Base/Mods` CE source.
2. **Modded damage sources** (abilities, gas, custom verbs) — covered automatically iff they pass a `DamageInfo` with an instigator through `TakeDamage`; sources that don't land in the unattributed line *until* a compat module registers an attribution handler for them (§4) — the designed escape hatch, no core patches per mod. Casualties/timeline still catch the resulting deaths via state transitions.
3. **Performance** — all collection is event-driven on combat events (already heavyweight code paths); end-polling is 1 check / 250 ticks while a session is active; zero cost when idle. The `TakeDamage` postfix early-outs on a static flag when no session is active. Timeline text rendering (`ToGameStringFromPOV`) happens once per notable event, not per frame.
4. **`BattleLog.Add` is also called for non-combat oddities** (social fight hits are `BattleLogEntry_MeleeCombat` too) — the hostility gate in §5 must stay the single source of truth for session starts; social fights between colonists must not open sessions (both sides player faction → conditions 2+3 fail).
5. **Identity rot at scale** — with all-faction tracking, *most* participants in an old dossier are dead and discarded. The snapshot-first rule (§3) is the firewall; the live-reference affordance is the only code path touching real Things and is null-guarded. Timeline strings are rendered at event time precisely because the grammar needs live pawns.
6. **Merge false positives** — accepted by design (§5): unrelated hostiles on the same map during a battle join the dossier. Cosmetic at worst; the faction filter separates them in the view.
7. **Long sieges/quests with persistent low threat** — the threat-clear end condition could hold sessions open for days; the 15000-tick safety cap bounds this. Sappers/sieges that lull >5000 ticks *between phases* may still split into two dossiers — matches vanilla's pawn-log grouping, accepted; the heuristic merge does not bridge time gaps (both sessions must be live simultaneously).
8. **Save bloat regression** — the unlimited default depends on the timeline cap and on not storing per-hit data. Any future "detailed combat replay" feature must be opt-in and separately bounded. Manual deletion + rolling window give players the escape hatch regardless.
9. **Building participant churn** — turrets are routinely destroyed and rebuilt; rows are per-instance (a rebuilt turret in a later battle is a new row in a new dossier), which is the natural reading. Within one battle a destroyed-then-rebuilt turret produces two rows; rare and harmless.

## 11. v1 Scope

1. `DossierManager` + session model keyed to sets of vanilla `Battle`s, with absorb-chain handling and same-map concurrent-battle merging.
2. Collection patches: `Thing.TakeDamage`, `Pawn.Kill`, `Pawn_HealthTracker.MakeDowned`, `Verb_Shoot.TryCastShot`, `Verb_MeleeAttack.TryCastShot`; `BattleLog.Add` postfix for session start, timeline, and casualties.
3. Attribution resolver pipeline (`AttributionResolver.Register` + `IAttributionHandler`) with the default instigator handler; all credit decisions routed through it.
4. All-faction participant tracking (pawns + buildings) with sides, snapshot-first identity, and fate classification.
5. Auto start via hostility gating; auto end via vanilla exit-window + threat-clear polling; no manual control.
6. `Window_BattleDossier` (Overview / Leaderboard with faction filter / Timeline) + dossier browser with pinning and per-entry deletion; `Letter_BattleEnded`.
7. Self-contained persistent dossiers, timeline cap, `maxStoredDossiers` rolling window (default unlimited).
8. Settings: trigger thresholds, end-cap ticks, letter behavior, rolling window, timeline cap.

Phase 2: scoring, MVP badging, mood/respect rewards (§8). Future beyond that: aggregate stats across dossiers (lifetime MVP counts, deadliest enemy faction), graph-tab integration, detailed-replay opt-in, `BattleDossier.CE` and other attribution compat modules as verification demands, declarative (`DefModExtension`) attribution registration, dossier text export.

## Appendix: Key Vanilla Anchors

| Concern | Anchor |
| ------- | ------ |
| Battle grouping/merge | `BattleLog.Add` (`Verse/BattleLog.cs:23`), `Battle.Absorb` (`Verse/Battle.cs:90`), `Pawn_RecordsTracker.BattleActive`/`EnterBattle` (`RimWorld/Pawn_RecordsTracker.cs:28,74`) |
| Battle-exit window | `Battle.TicksForBattleExit = 5000` (`Verse/Battle.cs:13`); battle trimming `BattleLog.ReduceToCapacity` (`Verse/BattleLog.cs:82`, 420000-tick retention) |
| Battle naming | `Battle.GetName()` grammar (`Verse/Battle.cs:52`), `RulePackDefOf.Battle_*` |
| Pawn-log headlines | `ITab_Pawn_Log_Utility.GenerateLogLinesFor` |
| Entry text rendering | `LogEntry.ToGameStringFromPOV` (`Verse/LogEntry.cs:52`) — used to freeze timeline sentences while pawns are alive |
| Damage amounts | `Thing.TakeDamage` → `DamageResult.totalDamageDealt` (`Verse/Thing.cs:908,927,939`), `DamageWorker.DamageResult` (`Verse/DamageWorker.cs:242`) |
| Kill/down sites | `Pawn.Kill` (`Verse/Pawn.cs:966`, side effects `:2777`), `Pawn_HealthTracker.MakeDowned` (`:840`); vanilla pawn-only credit via `RecordsUtility` (`RimWorld/RecordsUtility.cs:12,26`) — both sites always log a `BattleLogEntry_StateTransition` even without an instigator |
| Building/turret/explosion instigators | `Verb_LaunchProjectile` manning resolution (`:100-106`), `DamageWorker.ExplosionDamageThing` (`:212`), `Bullet.Impact` (`RimWorld/Bullet.cs:15`), `Building_TrapDamager.DamagePawn` (`RimWorld/Building_TrapDamager.cs:31`) |
| Threat detection | `GenHostility.AnyHostileActiveThreatToPlayer` (`RimWorld/GenHostility.cs:55`), `DangerWatcher` (`RimWorld/DangerWatcher.cs`, 101-tick interval) |
| Letters & archive | `ChoiceLetter.OpenLetter`, `Archive` culling/pinning (`RimWorld/Archive.cs:27,69`), `MainTabWindow_History` Messages tab |
| Phase-2 reward precedent | `DefeatedHostileFactionLeader(/Opinion)` defs (`Thoughts_Memory_Misc.xml:574-601`), `Thought_Tale` (`RimWorld/Thought_Tale.cs`), `TaleUtility.Notify_PawnKilled` tale recording (`RimWorld/TaleUtility.cs:45`), `TaleManager.GetLatestTale` (`:52`) |
| Records API (untouched) | `Pawn_RecordsTracker.AddTo/Increment` (`RimWorld/Pawn_RecordsTracker.cs:51,58`), `Verb_Shoot.TryCastShot` ShotsFired (`Verse/Verb_Shoot.cs:29`) |
