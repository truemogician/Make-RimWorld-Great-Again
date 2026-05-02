# ExactStorage

Use this file for ExactStorage-specific context only. For shared RimWorld-mod build, layout, and repo conventions, read [../AGENTS.md](../AGENTS.md).

## What This Mod Does

ExactStorage adds exact stock targets to RimWorld storage settings.

- Per-item and per-category minimum quotas create refill demand.
- Per-item and per-category maximum quotas prevent overfill.
- Quotas are evaluated internally in stock units, even when the UI accepts raw counts.
- Linked storages can optionally be evaluated separately, but only when all linked members are the same storage building type.

## Main Runtime Surfaces

- `Source/Initializer.cs`: main Harmony bootstrap.
- `Source/Manager.cs`: binds `StorageSettings` to `Profile` instances.
- `Source/Profile.cs`: persisted mod state for one storage profile.
- `Source/Quota.cs`: min/max rule keyed by `ThingDef` or `ThingCategoryDef`.
- `Source/StorageUtility.cs`: core storage, refill, capacity, and hauling logic.
- `Source/UI.cs`: storage-filter UI extension for toggles, quota editors, and summary bar.

## Compatibility Modules

`Public/LoadFolders.xml` conditionally loads three ExactStorage-specific integrations:

- `Source.PUAH/`: Pick Up And Haul compatibility, including enroute stock accounting and unload limiting.
- `Source.SRH/`: Storage Refill Hysteresis gating for under-min refill behavior.
- `Source.SSS/`: Save Storage Settings import/export support for ExactStorage profiles.

## ExactStorage-Specific Rules

1. Keep quota math in stock units.
2. Do not bypass vanilla storage acceptance, pathing, or forbid rules.
3. Be careful with linked-storage scope; `SeparateLinkedStorages` changes which parent/group gets counted.
4. Include enroute stock when changing haul destination or refill logic.
5. Preserve save compatibility in `Profile.ExposeData()` and `Quota.ExposeData()`.
6. Keep compatibility behavior isolated to its subproject unless a shared hook is truly required.

## Open These First

1. `Source/StorageUtility.cs`
2. `Source/Profile.cs`
3. `Source/Quota.cs`
4. `Source/UI.cs`
5. `Source/Patches/StorageBehaviorPatches.cs`
6. `Source/Patches/StorageSettingsPatches.cs`
7. `Source/Patches/StorageUIPatches.cs`
8. `TestScenarios.md`

Then branch into:

- `Source.PUAH/Patches/`
- `Source.SRH/Initializer.cs`
- `Source.SSS/ProfileFile.cs`
- `Source.SSS/Patches/IOPatches.cs`

## Validation

Use `TestScenarios.md` as the manual regression checklist.

Pay special attention to:

- min-only vs max-only quota behavior
- item and category quota interaction
- linked storage counting
- over-capacity summaries
- PUAH haul limiting and overbooking
- SRH refill suppression
- SSS profile import/export

## Useful Project Facts

- Root namespace: `TrueMogician.RimWorld.ExactStorage`
- Package ID: `TrueMogician.ExactStorage`
- Translation file: `Languages/English/ExactStorage.xml`