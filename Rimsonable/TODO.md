# Rimsonable Development Todo List

### ~~Grenades Through Shields~~
- **Description:** Grenades should be able to pass through shields, including bubble shields and shield belts.
- **Completion Date:** 2025-12-19
- **Mod Compatibility:**
    - [x] Combat Extended
    - [x] Vanilla Expanded Framework

### ~~Relevant Inspiration~~
- **Description:** Inspiration should be relevant to skill levels. Higher the skill, more likely to have a relevant inspiration.
- **Duplication:** [Inspiration Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=2117570018)

### Aircraft Fuel Consumption
- **Description:** Correct the fuel consumption of air vehicles (pods, planes and helicopters) from $kd$ to $k_1md + 2k_2m$, where $d$ is distance, $m$ is total mass, $k_1$ and $k_2$ are constants. This accounts for distance, mass, and takeoff/landing costs. $k_1$ and $k_2$ are specific to each vehicle type.
- **Mod Requirements:** Vehicle Framework
- **Mod Compatibility:**
    - [ ] Vanilla Vehicles Expanded
    - [ ] Vanilla Vehicles Expanded - Tier 3
- **Priority:** Medium
- **Difficulty:** Medium
- **Performance Impact:** Negligible

### Pods Vehicle Tail Flame
- **Description:** The tail flame of pod-type vehicles (Frog, Toad, Goliath) should be not only visual effects, but does actual damage, including setting up fires and heating up surrounding area.
- **Mod Requirements:** Vehicle Framework, Vanilla Vehicles Expanded
- **Mod Compatibility:**
    - [ ] Vanilla Vehicles Expanded - Tier 3
- **Priority:** Medium
- **Difficulty:** Medium
- **Performance Impact:** Low

### ~~Seeds and Transplanting~~
- **Description:** Planting should require seeds, which can be traded and harvested from mature plants. All trees and bushes can be transplanted, with a chance of survival based on plant type and planter's skill.
- **Duplication:** [SeedsPlease: Lite Redux](https://steamcommunity.com/sharedfiles/filedetails/?id=3523459853)

### Building Authorship
- **Description:** Buildings with quality levels should be tied to a specific pawn as builder, disallowing others to continue. Includes UI to set minimum skill levels or designate a specific pawn.
- **Priority:** Medium
- **Difficulty:** Medium
- **Performance Impact:** Low

### ~~Work Memory~~
- **Description:** When manufacturing items with quality levels, work speed follows a sigmoid curve based on when the pawn last worked on it, encouraging focused work.
- **Completion Date:** 2026-03-26
- **Priority:** High
- **Difficulty:** High
- **Performance Impact:** Medium (Requires optimization for WorkTicks)

### Deep Sleep
- **Description:** Rest recovery speed follows a sigmoid curve based on sleep duration. Natural wake-up gives a mood/work speed buff; forced wake-up gives a debuff.
- **Priority:** High
- **Difficulty:** High
- **Performance Impact:** Medium (Hooks into NeedInterval)

### ~~Safe Rest Location~~
- **Description:** Pawns should avoid sleeping at doors or in hazardous locations, including fire, pollution, gas, and corpses.
- **Completion Date:** 2026-01-03
- **Priority:** Medium
- **Implementation Difficulty:** Low
- **Performance Impact:** Low

### ~~Target Mark Enhancement~~
- **Description:** Artillery and mortars should prioritize firing at targets marked by spotters with binoculars. Other turrets gain a small accuracy boost when firing at marked targets.
- **Completion Date:** 2026-01-03
- **Mod Requirements:** Combat Extended
- **Priority:** Medium
- **Implementation Difficulty:** Low
- **Performance Impact:** Low

### ~~Auto Avoid Proximity Activators~~
- **Description:** Pawns not drafted should automatically avoid mechanoid detectors when moving around the map, similar to how they avoid fire. Applicable to pawns from all factions.
- **Completion Date:** 2026-03-21
### ~~Build at Corners~~
- **Description:** In vanilla game, the build blueprint at wall corners (the central tile of a + shape) can be hauled to, but not built on. This feature intends to fix that by allowing construction at corners.
- **Completion Date:** 2026-04-23