# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Assembler is a Unity 6 (6000.4.5f1) framework for defining and running games declaratively via YAML configuration files. Games are described as entities with composable behaviours, and a multi-stage pipeline transforms YAML into executable Unity GameObjects.

For *where the project is heading* — the mobile player-app product vision, the phased roadmap, and agreed-but-unbuilt design directions (remote loading, the phase system, determinism) — see [`../ROADMAP.md`](../ROADMAP.md). This file covers how to work in the code.

> **Note on the project root.** The git repo root is one level up (it also contains `RemoteTooling/` and the root `README.md`); the **Unity project is this `Assembler/` subdirectory**. `Tools/`, `Assets/`, and the `.sh` scripts all live under `Assembler/`. In a Claude worktree the layout is `<worktree>/Assembler/…`, so edit and run there — an absolute `/Users/.../Unity Projects/Assembler/Assets/…` path targets the user's **main checkout**, not your worktree (see Workflow → Git Worktrees).

## Code Conventions

- **Nullable reference types** are enabled project-wide (`Assets/Parsing/csc.rsp`). Respect the annotations in all new and modified code; avoid `null!` suppression unless justified.
- **Unity `.meta` files** are generated automatically by Unity — never hand-author them.
- **2D quantities are `Vector3` (z=0), not `Vector2`.** `Vector2Value` has been removed from the value pipeline; the `!vec` YAML tag produces `Vector3Value`, and domain code (sprite sizes, input axes/positions, etc.) uses `Vector3` throughout. Keep `Vector2` only at Unity API boundaries that force it (`RectTransform` anchors/offsets, `CanvasScaler.referenceResolution`, `InputAction.ReadValue<Vector2>`, `Random.insideUnitCircle`), widening to `Vector3` as values cross into domain code.

### C# Style

Favour modern C# and a functional style: records for immutable data (update with `with`, not mutation), switch expressions and pattern matching over if/else chains, LINQ pipelines and pure functions over imperative loops and mutable accumulation, expression-bodied members, `init`-only setters / `readonly` fields / `IReadOnlyList<T>` in signatures, guard clauses over deep nesting, target-typed `new()`, primary constructors for records. These are preferences, not absolutes — break them when clarity demands.

Mechanical formatting is enforced by **`dotnet format`** via the repo `.editorconfig` — run `Tools/check-format.sh`, or `--fix` to apply. Only `:warning`-severity rules are auto-fixed (indentation/whitespace + always-braces); `:suggestion` rules are IDE hints and never auto-applied. Roslyn has no max-width wrapping, so **your line breaks are preserved** — the formatter normalises violations rather than imposing a layout. Rider reads the same keys on save.

The project-specific rules `dotnet format` won't catch, and which are easy to get wrong:

- **C# 9 only — no record `struct`s.** `record struct` / `readonly record struct` are a C# 10 feature and won't compile. Use a `record` (reference type) for immutable data, or a plain `struct` where a value type is required — but a `struct` can't use `with`, so hand-copy fields instead. This is per-assembly, controlled by each assembly's `csc.rsp`: assemblies with only `-nullable:enable` get C# 9 (records/`init` also need an internal `IsExternalInit.cs` per assembly). **Raw string literals (`$$"""..."""`) additionally need `-langVersion:preview`** in that assembly's `csc.rsp` — `Assembler.Generation/csc.rsp` is the precedent (it has both). A missing `csc.rsp`/`IsExternalInit.cs` is a latent compile break that only surfaces when the assembly recompiles.
- **Unity object null checks must stay `== null` / `!= null`.** `UnityEngine.Object` overloads `==` to report destroyed objects as null, which `is null` bypasses. Everywhere else prefer `is` / `is not` for null, constant, and enum/type checks.
- **Null object pattern over nullable types** — prefer the existing `None<T>` / `NullValueProvider<T>` sentinels to returning or branching on `null`. Nullable reference types stay enabled and must be honoured where `null` genuinely crosses a boundary, but design to avoid that where practical.
- **Wrap the whole body of any `async void` method in try/catch** — an exception escaping an `async void` is unhandled (it can crash a player build and isn't caught by callers). `async void` is only for event handlers / Unity lifecycle callbacks that can't return a `Task`; everywhere else return `Task`.
- **Order members most-public-first** (public → internal → protected → private). Unity lifecycle methods (`Awake`, `OnEnable`, `Start`, `Update`, `FixedUpdate`, `OnDisable`, `OnDestroy`, etc.) are exempt and always come first, in lifecycle order. Convention only — no Roslyn/`.editorconfig` rule enforces it, so keep it in order by hand.
- **Route randomness through `RandomMath`** (or `SteeringMath`) in behaviours and libraries, never `UnityEngine.Random` directly. `RandomMath` draws from the seeded per-run PRNG; a direct call bypasses the run seed and won't replay. The same escape hatch exists in descriptor expressions — one naming `UnityEngine.Random.Range` by qualified name compiles and runs but is unseeded.

## Build & Test

This is a Unity project — there is no CLI build. Open in Unity Editor 6000.4.5f1.

**Run a game**: Editor menu `Assembler > Game Launcher` opens a window that auto-discovers every descriptor in `Assets/ExampleGameDescriptors/`, lets you pick one (and optionally simulate a target platform), and enters Play mode running it via `Builder.Build(yamlPath)`.

> **Use the `Tools/*.sh` scripts sparingly.** They each boot Unity in batch mode and are slow (tens of seconds to a couple of minutes). Only run one when you're genuinely unsure whether a change is correct — for routine edits where the code is obviously correct, skip the check and rely on the user's open editor to surface any issue. Prefer the cheapest applicable script and scope it to the files you touched rather than running `--all`.

**Each script documents its own modes, flags, and caveats in its header comment — read that rather than guessing.** All exit non-zero on failure, print a compact summary, and have an equivalent `Assembler >` editor menu item.

| Script | Checks | Cost |
|---|---|---|
| `check-expression.sh` | Expressions compile via `ExpressionMethodCompiler`, without booting a game. `-e '<code>'` snippets or a descriptor sweep | cheapest |
| `validate-yaml.sh` | Descriptor YAML *structure* only — well-formedness + duplicate keys | light |
| `check-compile.sh` | C# compiles; reports errors **and** warnings. Incremental by default; `--all` for a full audit | light |
| `run-tests.sh` | EditMode test suites; NUnit XML in `TestResults/`. Scope with assembly names, `--filter`, `--category` | medium |
| `validate-game.sh` | A descriptor actually *boots*: structure → deserialise → parse → resolve → instantiate, reported per stage | medium |
| `check-docs.sh` | Committed `Assets/docs/*.md` still match generated output (drift guard) | medium |
| `generate-docs.sh` | Regenerates `Assets/docs/Behaviours.md` + `Libraries.md` | medium |
| `check-format.sh` | `dotnet format` against `.editorconfig`; `--fix` writes | **heaviest** (Unity boot + MSBuild load) |

Test assemblies live in `Assets/Tests/` per area: `Tests.Compiler`, `Tests.Parsing`, `Tests.Behaviours`, `Tests.Generation`, `Tests.Voxels`, `Tests.Input`, `Tests.Resolving`.

**Concurrency, all scripts:** they run fine alongside an editor open on a *different* path (e.g. the user's main checkout), but refuse if an editor already has *this* path open. The first run in a fresh worktree does a one-time cold import (~3 min).

> **If a script gives a confusing result — a compile error you didn't cause, a pass that should have failed, a descriptor the Launcher won't show — read [`Tools/CLAUDE.md`](Tools/CLAUDE.md) before debugging it.** It documents the batch-mode traps (spurious `PackageCache` errors on a cold import, the `| tail` exit-code footgun, the `validate-game` baseline failure, the `com.unity.ai.assistant` DLL collision).

**Adding a registry package to `Packages/manifest.json`:** don't guess the version — read the editor's recommended `minimumVersion` from `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/Resources/PackageManager/Editor/manifest.json`. (For 6000.4.5f1, `com.unity.addressables` → `2.9.1`.) The resolver bumps transitive deps as needed.

## Architecture

### Pipeline Stages

YAML → **Deserialisation** (DTOs) → **Parsing/Transformation** (Info types) → **Resolving** (IValueProviders) → **Building** (GameObjects) → **Execution**

Each stage has its own assembly (`.asmdef`) and namespace under `Assembler.*`. Most assemblies carry a `README.md` with stage-specific authoring gotchas — read it before working in one.

### Assembly Structure

| Assembly | Namespace | Purpose |
|---|---|---|
| `Assembler.Deserialisation` | `Assembler.Deserialisation` | YAML parsing via YamlDotNet into DTO classes |
| `Assembler.Parsing` | `Assembler.Parsing` / `.Info` | Transforms DTOs into validated, strongly-typed Info records |
| `Assembler.Compiler` | `Assembler.Compiler.Compiler` | Lexer/parser for a C# expression subset (see `Assets/Compiler/COMPILER_SYNTAX_REFERENCE.md`) |
| `Assembler.Resolving` | `Assembler.Resolving` | Resolves `ValueSource<T>` → `IValueProvider<T>` at runtime |
| `Assembler.Building` | `Assembler.Building` | Orchestrates the full pipeline; `Builder.cs` is the entry point |
| `Assembler.Core` | `Assembler.Core` | `GameEntity` and `GameBehaviour<TData>` base MonoBehaviours |
| `Assembler.Behaviours` | `Assembler.Behaviours` | Concrete behaviour implementations (movement, physics, triggers, etc.). The composable uGUI UI blocks live under `Assembler.Behaviours.UI` |
| `Assembler.Input` | `Assembler.Input` | Input System wiring: `InputPlatform`, platform selection/fallback, controls validation, `InputActionBuilder` |
| `Assembler.Time` | `Assembler.Time` | Game clock abstraction (`IGameClock`, `RealtimeGameClock`, `FixedStepGameClock`) driving deterministic time |
| `Assembler.Libraries` | `Assembler.Libraries` | Static helper libraries callable from expressions (`VectorMath`, `RandomMath`, `ColorMath`, `GridMath`, `HexMath`, `NumberMath`, etc.) |
| `Assembler.Validation` | `Assembler.Validation` | Runtime YAML structure validator (`YamlStructureValidator`); platform-agnostic so a player build can validate descriptors |
| `Assembler.Extensions` | `Assembler.Extensions` | Shared extension methods (`VectorExtensions`, `EnumerableExtensions`, `GameObjectExtensions`) |
| `Assembler.Voxels` | `Assembler.Voxels` | Goxel `.txt` voxel format parsing/writing and coordinate conversion |
| `Assembler.Anthropic` | `Assembler.Anthropic` | Minimal HTTP client for the Anthropic Messages API |
| `Assembler.Generation` | `Assembler.Generation` | LLM-driven YAML game-descriptor generation; wraps `AnthropicClient` with a system prompt built from the behaviour catalogue |
| `Assembler.Generation.Verification` | `Assembler.Generation.Verification` | Generate → build → verify loop (`GenerationOrchestrator`, `BuildHarness`) that retries generation until a descriptor builds cleanly |

Note: `BehaviourRegistry` exists in two places — `Assembler.Parsing.BehaviourRegistry` is the *static catalogue* mapping behaviour names to factories (used during parsing), while `Assembler.Building.BehaviourRegistry` is the *runtime instance* registry mapping `BehaviourDescriptor` to live `GameBehaviour` components (used during wiring/execution).

### Three-Layer Type System

1. **DTOs** (`Assets/Deserialisation/Dtos/`): Raw deserialized YAML — `GameDto`, `EntityDto`, `BehaviourDto`, `ValueDto`
2. **Info records** (`Assets/Parsing/Info/`): Validated, immutable records — `GameInfo`, `EntityInfo`, `BehaviourInfo` subclasses. Values are wrapped as `ValueSource<T>` (abstract) with concrete subtypes: `ConstantSource<T>`, `ValueReferenceSource<T>`, `ExpressionSource<T>`, `AssetSource<T>`, `TriggerOutputSource<T>`, `None<T>`
3. **Runtime providers** (`Assets/Resolving/`): `IValueProvider<T>` implementations that supply values during gameplay — `ValueProvider<T>`, `ExpressionValueProvider<T>`, `TriggerOutputProvider<T>`, `NullValueProvider<T>`

### Behaviour System

All behaviours are registered in `BehaviourRegistry.All` (`Assets/Parsing/BehaviourRegistry.cs`) as a dictionary mapping string names (e.g. `"velocity"`, `"key hold trigger"`) to factory functions and property descriptors. Each behaviour type is five files — **use the `add-behaviour` skill to add one** rather than wiring it by hand.

Behaviours communicate via a listener/observer pattern: triggers notify downstream behaviours through `Action` delegates. `ListenerInfo` has three variants (`Assets/Parsing/Info/ListenerInfo.cs`):

- `DirectListenerInfo` — targets a specific behaviour by `BehaviourDescriptor` (entity ID + behaviour ID)
- `EntityTaggedListenerInfo` — targets behaviours on any entity matching an entity tag (optionally filtered by behaviour ID)
- `BehaviourTaggedListenerInfo` — targets any behaviour matching a behaviour tag, regardless of entity

Tag values are `ValueSource<string>`, so they can be constants, references, or expressions resolved at runtime. See `Assets/ExampleGameDescriptors/TaggedListenerDemo.yaml` for example usage.

### Two-Phase Initialization

Building uses deferred initialization. `GameBehaviourFactory.Create()` returns a tuple of `(GameBehaviour, InitialiseBehaviourEvent)`. All behaviours for all entities are created first, then the `InitialiseBehaviourEvent` delegates are executed afterwards via `InitialisationQueue.ExecuteAll()`. This is necessary because listeners reference other behaviours by `BehaviourDescriptor` (entity ID + behaviour ID), which must all be registered in the `BehaviourRegistry` before wiring.

### Key Entry Points

- `Builder.Build(yamlPath)` — end-to-end pipeline from a YAML descriptor to a running game (also bootstraps the UI `EventSystem` and prefab library)
- `Transformer.Transform()` — converts `GameDto` → `GameInfo`
- `GameEntityFactory.Create()` — instantiates a single entity with all its behaviours
- `GameBehaviourFactory.Create()` — maps `BehaviourInfo` type to concrete `GameBehaviour` component
- `ValueResolver.Resolve()` — extension method converting `ValueSource<T>` to `IValueProvider<T>`
- `TemplateInstantiator` — expands templates with parameter substitution

### Expression Compiler

`Assets/Compiler/` contains a custom lexer/parser that compiles a C# subset into delegates at runtime — arithmetic, comparison, control flow, method calls, `new` expressions, lambdas, and LINQ. See `Assets/Compiler/COMPILER_SYNTAX_REFERENCE.md` for the full language specification, and the `unity-expression-compiler` skill when authoring expressions.

### Game Definitions

Example YAML game files live in `Assets/ExampleGameDescriptors/` (`Pong.yaml`, `Snake 2.yaml`, `Asteroids.yaml`, `Tetris.yaml`, `FlappyBird.yaml`, `TaggedListenerDemo.yaml`, `UiShowcase.yaml`, …). They define metadata, assets, constants, variables (including list-typed, e.g. `!vec []`), templates, entities (with behaviours and listeners), and expressions. Schema reference: `Assets/docs/GameDescriptorSchema.md`; behaviour and library catalogues: `Assets/docs/Behaviours.md` / `Libraries.md`.

IDs for definitions (entities, behaviours, templates, variables, etc.) are promoted to YAML keys at the definition site rather than being a separate `id:` property — the mapping key *is* the identifier.

## Workflow

### Committing & Pushing

- **Always commit and push at the end of a session's work** — if a branch exists for the session's work, commit any outstanding changes and push at the end without asking.
- **The user's main checkout often has files pre-staged** — frequently hundreds of already-staged entries (MeshyImports, TextToVoxel, `.gitattributes`, the untracked Sirenix module below). A bare `git commit` after `git add <one path>` snapshots the *whole* index; this once produced a 347-file accident commit. When committing in the main checkout, **always use an explicit pathspec** (`git commit -m … -- <paths>`) and check `git status --short` for leading `A`/`M` first. Large pushes can exceed the 2-min Bash timeout — push with a longer timeout and verify with `git ls-remote` after a killed push (it may not have landed).

### Addressing PR comments

PR feedback lives in **three separate streams**, and you must fetch all three — inline review comments do NOT include top-level conversation comments, so it's easy to silently miss a whole category of feedback:

- **Inline review comments** (attached to a file/line): `gh api repos/<owner>/<repo>/pulls/<n>/comments`
- **Top-level conversation comments**: `gh api repos/<owner>/<repo>/issues/<n>/comments` — PRs are issues
- **Review summaries** (the body of an approve/request-changes review): `gh pr view <n> --json reviews`

Enumerate them explicitly so none are dropped, and reply on the PR mapping each comment to its resolution.

**If `gh` GraphQL 401s mid-session** (`HTTP 401: Requires authentication (…/graphql)`) while `git push` and REST still work, don't re-auth — `gh pr edit` / `gh pr comment` use GraphQL, but REST works: edit with `gh api -X PATCH repos/<owner>/<repo>/pulls/<n> -F body=@file.md`, comment with `gh api -X POST repos/<owner>/<repo>/issues/<n>/comments -F body=@file.md`. Prefer the REST route before asking the user to `gh auth refresh`.

### Git Worktrees

AI work happens in a worktree so the user can keep using the main repo. The flow is: work in a worktree → open a PR → the user checks the branch out in the main repo to run it in Unity. So the worktree must be removed once a PR exists and recreated when more work is requested.

- **Before starting work on a branch**: check whether its worktree exists; recreate it if not. Never work directly in the main repo — the user keeps that checkout for running in Unity.
- **When a PR is created**: after pushing and opening the PR, commit any remaining changes, then `git worktree remove`. Tell the user the branch is ready to check out.
- **Follow-up work**: recreate the worktree first, then continue in it; remove it again once the PR is updated.

**Gotchas when working in a worktree:**

- **Never `git add -A` / `git commit -am`.** The worktrees carry untracked local state that is not on master — a whole `Assets/Plugins/Sirenix/Odin Inspector/Modules/Unity.Addressables/` module (~3k lines) plus a modified `OdinModuleConfig.asset`. `git add -A` sweeps it into your commit (this happened on PR #377). Stage only the explicit files you changed and verify with `git diff --cached --name-only | grep -i sirenix` (should be empty). For the same reason, do **not** force-remove a worktree while that untracked Sirenix state is present — it's the user's; leave it and tell them.
- **Subagents reading absolute paths land in the main checkout, not your worktree.** The main checkout is frequently on a newer, unmerged branch, so agents may report APIs that don't exist on your base. The worktree's Unity project is *nested* (`<worktree>/Assembler/Assets/…`) while the main checkout has `Assets/` at its root — so an absolute path from an agent report edits the *main* checkout (symptom: `git -C <worktree> status` stays clean and your changes aren't there). **Re-Read the exact files from the worktree path before editing.**
- **Validating a branch the user's editor holds.** After a PR is opened the worktree is removed and the branch is often checked out in the main repo with Unity open — so `Tools/*.sh` can't run there, and you can't `git worktree add` the branch twice. Validate in a throwaway detached worktree at a *different* path, fed by a staged patch: `git add -A` in the main checkout (stages without touching the working tree, so the editor is undisturbed) → `git worktree add --detach /tmp/<name> HEAD` → `git diff --cached --binary -- Assembler/ > /tmp/x.patch` → `git -C /tmp/<name> apply /tmp/x.patch` → run `Tools/*.sh` from `/tmp/<name>/Assembler` → `git worktree remove --force` when done.
