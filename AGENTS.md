## Repository Overview

Monorepo of active RimWorld 1.6 mods and support tools. Most projects target .NET Framework 4.7.2 with `LangVersion=latest`, share the `Utility` library, and inherit common MSBuild configuration from `Directory.Build.props`.

**Active projects:**
- **Rimsonable** - Flagship collection of individually toggleable vanilla-behavior fixes, with CE, RimHUD, and VEF compatibility modules.
- **Rimfined** - QoL and UI improvements, with CE and Vehicle Framework compatibility modules.
- **AnimalHaulExtended** - Animal hauling overhaul with preset-driven and custom work-giver selection.
- **ExactStorage** - Storage filter extension with per-item and per-category stock targets, plus a Storage Refill Hysteresis compatibility module.
- **RemedialAlgorithms** - Focused performance fixes and optimization experiments.
- **Profiler** - Harmony-based in-game profiling and diagnosis utility.

## Build & Run

Projects live in a few different layouts. Build from the repo root using the project file that matches the mod you are touching.

```bash
# Source-based gameplay mods
dotnet build Rimsonable/Source/Rimsonable.csproj
dotnet build Rimfined/Source/Rimfined.csproj
dotnet build AnimalHaulExtended/Source/AnimalHaulExtended.csproj
dotnet build ExactStorage/Source/ExactStorage.csproj

# Root-project layouts
dotnet build RemedialAlgorithms/RemedialAlgorithms.csproj
dotnet build Profiler/Profiler.csproj

# Compatibility modules
dotnet build Rimsonable/Source.CE/Rimsonable.CE.csproj
dotnet build Rimsonable/Source.RimHUD/Rimsonable.RimHUD.csproj
dotnet build Rimsonable/Source.VEF/Rimsonable.VEF.csproj
dotnet build Rimfined/Source.CE/Rimfined.CE.csproj
dotnet build Rimfined/Source.VF/Rimfined.VF.csproj
dotnet build ExactStorage/Source.SRH/ExactStorage.SRH.csproj
```

Useful supporting commands:

```bash
dotnet clean ExactStorage/Source/ExactStorage.csproj
dotnet clean Profiler/Profiler.csproj

# Launch scripts are generated into the project's build output
Rimsonable/Source/bin/Debug/net472/LaunchRimWorld.bat
ExactStorage/Source/bin/Debug/net472/LaunchRimWorld.bat
Profiler/bin/Debug/net472/LaunchRimWorld.bat
```

Shared build configuration comes from `Directory.Build.props`:
- `GameDir` defaults to the checked-in local `RimWorld/` folder.
- `SaveDataDir` defaults to `SaveData/`.
- `SteamGameDir` can be overridden for Steam deployment targets.
- Local overrides belong in `Directory.Build.user.props`.

Typical build pipeline for active mods:
1. Copy assets from `Public/` into `Dist/`.
2. Expand `{{Token}}` placeholders in `Template/About.xml` and generate runtime `ModsConfig.xml`.
3. Process translations from `Languages/` into `Dist/Shared/Languages/` when present.
4. Bundle or copy assemblies into `Dist/.../Assemblies/`.
5. Create a Windows junction from `Dist/` into the local RimWorld `Mods/` folder.
6. Generate `LaunchRimWorld.bat` in the build output directory.
7. Some projects also expose `DeployToSteam` / `RemoveFromSteam` targets.

There are no tests or linters configured.

## Shared Infrastructure (`Utility/`)

Active mods reference `Utility/Utility.csproj`. Important shared pieces:

**`FeatureSettings<T>`** - Base class for flag-driven mod settings. Handles Harmony patch registration and unpatching, settings UI rendering via `Listing_Standard`, and persistence through `Scribe_Values` with camelCase keys.

**`[Feature]` attribute** - Applied to `Features` enum fields. Declares `ModDependencies`, `ModIncompatibilities`, and `DefaultEnabled`. `FeatureSettings<T>` discovers related patches and settings rows via reflection.

**`[Translation]` attribute** - Maps C# members to RimWorld translation XML keys. Type-level use with `ImplicitMembers = true` auto-derives member keys.

**`Logger`** - Debug logger with a per-mod prefix. Enabled by default in `#if DEBUG`. Methods: `Message()`, `Warning(once:)`, `Error(once:)`.

**`Flexbox`** - UI layout helper that partitions `Rect` values into child rects using `fr`, `px`, and auto sizing. Shared settings UIs rely on this heavily.

**`Utility/Extensions` and `Utility/GUI`** - Common helpers for rect math, drawing, translated member lookup, and settings UX.

**MSBuild Tasks** (`Utility/Tasks/`) - `BuildXml`, `ProcessTranslations`, and `CreateLink`, invoked directly from project files.

## Architecture Patterns

### Project layouts

- **Source-based gameplay mods** keep the main assembly in `Source/` and optional integrations in sibling `Source.*` folders. This is the pattern used by `AnimalHaulExtended`, `ExactStorage`, `Rimfined`, and `Rimsonable`.
- **Root-project layouts** keep the `.csproj` at mod root and the runtime code in a same-name subfolder. `Profiler/Profiler.csproj` + `Profiler/Profiler/` and `RemedialAlgorithms/RemedialAlgorithms.csproj` + `RemedialAlgorithms/RemedialAlgorithms/` follow this layout.
- **Compatibility assemblies** use suffix folders such as `Source.CE`, `Source.RimHUD`, `Source.SRH`, `Source.VEF`, and `Source.VF`, and may distribute into nested `Dist/` subtrees.

### Settings styles

Two settings patterns are active in this repo:

```csharp
[Flags]
[FeaturesEnum(defaultEnabled: true)]
public enum Features : ulong {
    [Feature(ModDependencies = [ModIds.SomeMod])]
    MyFeature = 1ul << 0,
}

public sealed class Settings : FeatureSettings<Features> {
    static Settings() {
        AddFeaturePatches(Features.MyFeature, typeof(MyFeaturePatch));
    }
}
```

- `Rimsonable` and `Rimfined` use the flag-driven `FeatureSettings<Features>` model with hot-toggleable features.
- `AnimalHaulExtended`, `ExactStorage`, and `Profiler` use bespoke `ModSettings` or direct Harmony bootstrap when a per-feature flag system is not the right fit.

### Harmony patches

Attribute-based declaration is still the default, grouped under `Patches/` in both source-based and root-project layouts:

```csharp
[HarmonyPatch(typeof(TargetClass), "MethodName")]
private static bool MethodName_Prefix(ref int __result) { ... }
```

Compatibility shims usually register in `Initializer` classes inside their respective `Source.*` assemblies.

### Mod entry points

Feature-driven mods usually load settings and defer patch application through a queued long event:

```csharp
public class Mod : Verse.Mod {
    public Mod(ModContentPack content) : base(content) {
        Settings.Default = GetSettings<Settings>();
        LongEventHandler.QueueLongEvent(() => Settings.Default.Apply(), ...);
    }

    public override void DoSettingsWindowContents(Rect inRect) =>
        Settings.Default.DrawContents(inRect);

    public override string SettingsCategory() => ThisAssembly.Info.Title;
}
```

Simpler mods sometimes patch immediately in the constructor:

```csharp
public class Mod : Verse.Mod {
    public Mod(ModContentPack content) : base(content) {
        new Harmony(ThisAssembly.Project.PackageId).PatchAll();
    }
}
```

### Assembly metadata

Use `ThisAssembly.Info.*` and `ThisAssembly.Project.*` for generated metadata such as title, version, and package ID.

### Distribution layout

| Directory    | Purpose                                                                                                                                                 |
| ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Public/`    | Source assets such as Def XML, `LoadFolders.xml`, and textures.                                                                                         |
| `Template/`  | `About.xml` and `ModsConfig.xml` templates with `{{Token}}` placeholders.                                                                               |
| `Languages/` | Translation source XMLs consumed by `ProcessTranslations`.                                                                                              |
| `Dist/`      | Packaged output that mirrors the RimWorld mod folder structure. Compatibility assemblies may add nested subfolders such as `CE`, `SRH`, `VEF`, or `VF`. |

## Namespaces & Conventions

- Most gameplay namespaces follow `TrueMogician.RimWorld.<ModName>`.
- Compatibility assemblies append suffixes such as `.CE`, `.RimHUD`, `.SRH`, `.VEF`, and `.VF`.
- Shared library namespace: `TrueMogician.RimWorld.Utility`.
- `ModIds.cs` stores package ID constants when dependency or incompatibility declarations are needed.
- Settings persistence generally uses camelCase `Scribe_Values` keys, though standalone settings classes may use project-specific field names.
- Translation keys usually follow `<ModName>.Settings.<Path>` and are resolved through `[Translation]` attributes or explicit string prefixes.
- Read `TODO.md`, `Ideas.md`, and mod-specific `README.md` files before proposing new work so you do not duplicate an already-planned feature.

## Key Files Per Active Project

- `Source/Mod.cs` or `<ModName>/Mod.cs` - Main entry point.
- `Source/Settings.cs` or `<ModName>/Settings.cs` - Settings wiring and feature registration.
- `Source/Patches/` or `<ModName>/Patches/` - Harmony patch implementations.
- `Source/Components/`, manager classes, or data folders such as `Data/` - Runtime state and supporting systems.
- `Source.*` folders - Compatibility shims for specific upstream mods.
- `Public/LoadFolders.xml` - Conditional assembly loading for compatibility modules when the project uses it.
- `Template/About.xml` - Mod metadata template.
- `README.md`, `TODO.md`, and `Ideas.md` - Current intent, feature backlog, and design notes.

## Knowledge Base (`Knowledge Base/`)

This directory is Git-ignored but safe to explore freely. It is the primary reference for RimWorld modding:

- `Knowledge Base/Decompiled` and `Knowledge Base/Definition` - Authoritative vanilla RimWorld 1.6 API and behavior reference.
- `Knowledge Base/Mod Repository` - Reference implementations from mods such as Combat Extended and Vehicle Framework.
- `Knowledge Base/Steam Workshop` - Locally installed workshop mods for behavior comparisons and compatibility research.

Always consult the decompiled source when implementing patches or working with game types.

## Agent Configuration

`.github/agents/` contains specialized workflows for this repo:
- `Code Review.agent.md` - Scope-isolated, API-verified code review.
- `New Feature.agent.md` - Style-mimicking feature implementation.
- `Refactor.agent.md` - Focused refactor workflow.
- `Translation.agent.md` - Translation-specific workflow.
