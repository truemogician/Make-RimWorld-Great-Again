# Rimfined Development Todo List

### Force Work Overwrite
- **Description:** When forcing a pawn to work, allow overwriting if the target item/construction is reserved by another pawn. Auchtug! only allows reservation overwriting if the both work types are the same.

### Global Fire Control
- **Description:** Add a command to control whether pawns, turrets and vehicles can fire at will. Also add a command for "Search and Destroy" if this mod is installed.
- **Mod Compatibilty:**
  - [ ] Combat Extended
  - [ ] Vehicle Framework
  - [ ] Dubs Mint Menu
  - [ ] Search and Destroy

### ~~Construction Priority~~
- **Description:** Add a gizmo on construction blueprints to set construction priority. Pawns will prioritize higher priority blueprints when choosing what to build next, similar to the priority mechanism in Smart Farming.
- **Completion Date:** 2026-04-24

### Build Plan Overrides
- **Description:** Allow new build plans to override existing plans, if the buildings are of the same size and type.

### Don't Ignore Equipped Weapons
- **Description:** The "Drop Undefined" option for loadouts currently applies to equipped weapons as well, meaning enabling it without a weapon in the loadout will make pawns unequip their weapons. Change this behavior to only unequip weapons if there is a weapon in the loadout.

### No Corpse Auto-Forbid
- **Description:** Don't auto-forbid corpses of enemies/animals or apparels and items dropped by dead enemies/neutrals.
- **Mod Compatibilty:**
  - [x] Non Uno Pinata

### ~~No Target~~
- **Description:** Add a gizmo to enemy pawns to mark them as "No Target", preventing turrets and vehicles from firing at them. Enemies with relationships to colony pawns are automatically marked as "No Target".
- **Completion Date:** 2026-01-06
- **Mod Compatibilty:**
  - [x] Combat Extended
  - [x] Vehicle Framework
  - [x] Search And Destroy

### ~~Capture As A Job~~
- **Description:** Add a gizmo to downed pawns to mark them for capture so that players don't have to draft a colonist and manually capture them. Should fall under the "Warden" work type.
- **Completion Date:** 2026-01-04

### ~~Ambrosia Auto Harvest~~
- **Description:** Automatically mark ambrosia plants for harvest when they are fully grown.
- **Completion Date:** 2026-03-21

### ~~Ship Chunk Auto Deconstruct~~
- **Description:** Automatically mark ship chunks (including gravship wreckage) for deconstruction when they appear.
- **Completion Date:** 2026-05-02

### ~~Delayed Quest Acceptance~~
- **Description:** Add a button in the quest dialog to set the time to accept the quest. When clicked, it should show a list of time options (e.g. 1 day, 3 days, 1 week, right before expiration) and a "Cancel" button. The quest will be automatically accepted after the selected time has passed, or can be accepted manually before that.
- **Completion Date:** 2026-04-24

### ~~Pending Passenger~~
- **Description:** Show pawns entering a vehicle on the passenger list with a "pending" status, and a "cancel" button next to it.
- **Mod Requirements:** Vehicle Framework
- **Completion Date:** 2026-05-04

### UI Improvements
- **Inventory Tab:** Show items equipped by pawns in a separate tab on caravan/trade UI. The original tabs only show cargo inventory.
- **Closer Buttons:** Reorder the columns of the item list UI to move the +/- buttons closer to the title.

### Job Refusal Reasons
- **Description:** When the player wants to assign a forced job to a pawn, the vanilla game usually displays a greyed-out menu with a reason when a pawn can't perform a job. But this doesn't cover all cases. Sometimes, the menu simply doesn't show such an option without any explanation, which can be confusing for players. This feature intends to extend the vanilla job refusal reason system to cover as many cases as possible.

### No Auto-Draft
- **Description:** In vanilla, when a colonist is forcibly assigned jobs like rescuing or capturing, they will be automatically drafted, and the player need to manually undraft them after they finish the job. This feature aims to disable the auto-drafting behavior in such cases.

### Out Of Ammo Alert
- **Description:** Add a new alert, "Out of Ammo", which is triggered when a pawn's equipped weapon runs out of ammo.
- **Mod Requirements:** Combat Extended