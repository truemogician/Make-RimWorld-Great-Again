## Repository Overview

RimWorld 1.6 mod monorepo. Projects are C#/.NET Framework with `LangVersion=latest`; most target `net472`, while some compatibility/fix modules target `net48`. Shared build paths come from `Directory.Build.props`, and most mods reference `Utility/Utility.csproj`.

Read the nearest `AGENTS.md` first. Project-specific files currently exist for `Rimsonable/` and `ExactStorage/`.

## Current Projects

Buildable projects are discovered by `*.csproj`; do not assume every folder is active.

- Gameplay mods: `AnimalHaulExtended`, `ExactStorage`, `PriorityLoadController`, `Rimfined`, `Rimsonable`, `WorkMemory`.
- Performance/tools: `RemedialAlgorithms`, `Profiler`.
- Compatibility/fix modules: `AnimalHaulExtended.PUAH`, `ExactStorage.PUAH/SRH/SSS`, `Rimfined.CE/NUP/VF`, `Rimsonable.CE/VEF`, `WorkMemory.RimHUD`, `Fixes/UsefulMarksInColonyGroups`.
- Shared infrastructure: `Utility`.

## Build & Run

Build from the repo root with the project file you are touching:

```bash
dotnet build Rimsonable/Source/Rimsonable.csproj
dotnet build ExactStorage/Source/ExactStorage.csproj
dotnet build WorkMemory/Source/WorkMemory.csproj
dotnet build Fixes/UsefulMarksInColonyGroups/Source/UsefulMarksInColonyGroups.csproj
```

Use `dotnet clean <project.csproj>` for clean output. There are no automated tests or linters; use build output and any project-specific manual checklists such as `ExactStorage/TestScenarios.md`.

Builds typically copy `Public/` assets to `Dist/`, expand `Template/` XML tokens, process `Languages/`, place assemblies under the packaged mod layout, link `Dist/` into `RimWorld/Mods/`, and generate `LaunchRimWorld.bat` in the build output.

Important properties:

- `GameDir` defaults to local `RimWorld/`.
- `SaveDataDir` defaults to `SaveData/`.
- `SteamGameDir` can be overridden in `Directory.Build.user.props`.

## Layout & Conventions

- Main source-based mods use `Source/`; optional integrations use `Source.*` folders.
- Root-project layouts keep the `.csproj` at project root and code in a same-name folder, e.g. `Profiler/Profiler/`.
- Distribution folders: `Public/` source assets, `Template/` tokenized XML, `Languages/` translations, `Dist/` packaged output.
- Namespaces usually follow `TrueMogician.RimWorld.<ModName>`; compatibility assemblies append suffixes such as `.CE`, `.PUAH`, `.RimHUD`, `.SRH`, `.SSS`, `.VEF`, or `.VF`.
- Follow `.editorconfig`: tabs, max line length 150, K&R braces.
- Use `ThisAssembly.Info.*` and `ThisAssembly.Project.*` for generated metadata.
- Keep comments sparse and useful. Prefer direct local code over thin wrappers or unnecessary abstraction.

## RimWorld Modding Patterns

- Harmony patches are usually attribute-based and grouped under `Patches/`.
- Compatibility modules normally register through an `Initializer` class and `Public/LoadFolders.xml`.
- `Rimsonable` and `Rimfined` use `FeatureSettings<Features>` for hot-toggleable feature patches.
- Other mods may use bespoke `ModSettings`, manager classes, or direct Harmony bootstrap.
- Persisted data usually uses `Scribe_Values` / `IExposable`; ask before changing save formats, settings keys, config shape, or other external contracts.
- Before proposing new work, check repo/project `TODO.md`, `Ideas.md`, `README.md`, and relevant test scenario docs.

## Investigation & Debugging

- Do not keep expanding static source-code searches indefinitely. If static analysis stops producing clear evidence, stop and summarize findings, unknowns, and the blocked question.
- Ask the user for guidance before continuing. A good next step is often targeted instrumentation in relevant methods, then an in-game test run by the user.
- When adding instrumentation, log concrete runtime facts: method entry/exit, key inputs, selected branches, counts, map/pawn/thing identifiers, and unexpected null or state transitions.
- Tell the user exactly which scenario to run and which logs to return. Wait for those logs before the next iteration.
- Remove temporary logs or gate them behind existing debug logging before finalizing, unless the user asks to keep them.

## Shared Utility (`Utility/`)

Use existing utility pieces when they fit naturally:

- `FeatureSettings<T>`, `[Feature]`, and `[FeaturesEnum]` for flag-driven settings and patch registration.
- `[Translation]` and `TranslationProvider` for translation key lookup.
- `Logger` for debug logging.
- `Flexbox`, `Utility/Extensions`, and `Utility/GUI` for settings UI and rect helpers.
- `Utility/Tasks/` MSBuild tasks: `BuildXml`, `ProcessTranslations`, `CreateLink`.

## Knowledge Base Policy

`Knowledge Base/` is Git-ignored but safe to read and is the primary RimWorld reference corpus.

- `Knowledge Base/Decompiled` and `Knowledge Base/Definition`: authoritative vanilla RimWorld 1.6 API and behavior reference.
- `Knowledge Base/Mod Repository`: reference implementations from mods such as Combat Extended and Vehicle Framework.
- `Knowledge Base/Steam Workshop`: locally installed workshop mods for behavior comparisons and compatibility research.

Always consult `Knowledge Base/Decompiled` or `Knowledge Base/Definition` before implementing patches or reasoning about vanilla game types.

### Decompiled Source Rule

- Never run decompilers on RimWorld DLLs or assemblies under `RimWorld/` to inspect vanilla game code.
- Use `Knowledge Base/Decompiled` instead; it is curated, authoritative, and has better semantics than fresh decompiler output.
- Do not invoke ILSpy, dnSpy, dotPeek, command-line decompilers, IDE decompiler views, or scripts that regenerate decompiled C# for RimWorld assemblies.
- This prohibition still applies when context about `Knowledge Base/` was missed. There is no fallback decompilation path for vanilla code because it is fully available under `Knowledge Base/Decompiled`.
- Missing-source scenarios usually involve integration or dependency mods, not vanilla RimWorld. If needed third-party mod source is missing or incomplete, ask the user with the built-in question tool when available; otherwise stop the iteration and explicitly state which mod source, type, or member is missing.
- If vanilla API or behavior is needed, search `Knowledge Base/Decompiled` and `Knowledge Base/Definition` first. Re-decompiling the same DLLs wastes time and can reduce accuracy.

## Agent Configuration

`.github/agents/` contains specialized workflows: `Code Review`, `Description`, `Design`, `Investigation`, `New Feature`, `Optimization`, `Refactor`, and `Translation`.
