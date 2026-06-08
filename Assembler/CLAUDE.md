# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Assembler is a Unity 6 (6000.4.5f1) framework for defining and running games declaratively via YAML configuration files. Games are described as entities with composable behaviours, and a multi-stage pipeline transforms YAML into executable Unity GameObjects.

## Code Conventions

- **Be concise** in all responses — favour short, direct answers and avoid unnecessary preamble or repetition.
- **Nullable reference types** are enabled project-wide (`Assets/Parsing/csc.rsp`). All new and modified code must respect nullable annotations — use `?` for nullable references, avoid `null!` suppression unless justified, and handle nullability properly.
- **Unity `.meta` files** never need to be created manually — Unity generates them automatically. Don't author or hand-create `.meta` files for new assets or scripts.
- **2D quantities are `Vector3` (z=0), not `Vector2`.** The `Vector2Value` type has been removed from the value pipeline; the `!vec` YAML tag produces `Vector3Value`, and domain code (sprite sizes, input axes/positions, etc.) uses `Vector3` throughout. Keep `Vector2` only at Unity API boundaries that force it (`RectTransform` anchors/offsets, `CanvasScaler.referenceResolution`, `InputAction.ReadValue<Vector2>`, `Random.insideUnitCircle`), widening to `Vector3` as values cross into domain code.

### C# Style

Favour modern C# and a functional style. These are preferences, not absolutes — break them when clarity demands it. The mechanical parts are enforced by **`dotnet format`** (built into the .NET SDK) via the repo `.editorconfig` — run `Tools/check-format.sh` to verify, or `Tools/check-format.sh --fix` to apply. Enforcement is controlled by **severity**: editorconfig rules at `:warning` are auto-fixed (indentation/whitespace + always-braces), rules at `:suggestion` are IDE hints only and are never auto-applied. Roslyn has no max-width wrapping, so the formatter *preserves your line breaks* and only normalises violations rather than imposing a layout. Rider reads the same keys on save.

- **Use modern language features** available on this Unity/C# version rather than older equivalents.
- **Records** for immutable data types (e.g. the Info records) — prefer them over hand-written classes where a value type fits. Update with `with` expressions, not mutation.
- **Null object pattern over nullable types** — prefer a sentinel/null-object (the existing `None<T>` / `NullValueProvider<T>` pattern) to returning or branching on `null`. Nullable reference types stay enabled and must be honoured where `null` genuinely crosses a boundary, but design to avoid that where practical.
- **Switch expressions** over switch statements and if/else chains; lean on **pattern matching** generally (property, relational, and `is` patterns), not just type switches.
- **Functional style** — prefer pure functions and LINQ pipelines over imperative loops and mutable accumulation; avoid hidden side effects.
- **Expression-bodied members** (`=>`) wherever a method/property/ctor is a single expression.
- **Ternary expressions** over short if/else when assigning or returning a value.
- **Immutability by default** — `init`-only setters, `readonly` fields, and `IReadOnlyList<T>`/`IEnumerable<T>` in signatures over mutable collection types.
- **Guard clauses / early returns** over deep nesting.
- **Target-typed `new()`** to cut redundant type noise.
- **`var`** for obvious types, `nameof`, and string interpolation throughout.
- **Primary constructors** for records.

## Build & Test

This is a Unity project — there is no CLI build. Open in Unity Editor 6000.4.5f1.

- **Run game**: Unity Editor menu `Assembler > Game Launcher` opens a window that auto-discovers every descriptor in `Assets/ExampleGameDescriptors/`, lets you pick one (and optionally simulate a target platform), and enters Play mode running it via `Builder.Build(yamlPath)`.
- **Check compilation**: run `Tools/check-compile.sh` for a fast headless check that the project's C# compiles — it surfaces compiler **errors and warnings** without running the (slower) test suite, so it's the quickest way to verify a code change before committing. It boots Unity in batch mode, parses the compiler diagnostics (`error CS…` / `warning CS…`) out of the log, prints a short `Compile check` summary, and exits non-zero on any error (or, with `--warnings-as-errors`/`-w`, on any warning). It parses the log rather than collecting messages in C# because Unity's `CompilationPipeline` callbacks don't reliably deliver warnings in batch mode. Errors are reported wherever they occur; warnings are filtered to your own code under `Assets/` (excluding `Assets/Plugins`) to avoid third-party noise. **Default (incremental):** a `-batchmode -quit` boot recompiles only what changed on disk since the last compile, so the report is scoped to the code you just edited and its dependents — not the project's ~50 pre-existing nullable warnings. **`--all`:** invokes `Editor.CompileCheckBatch.RecompileAll` to force a clean recompile of every assembly so all warnings resurface (slower; for a full audit). Same concurrency rules as the other scripts (runs alongside an editor on a *different* path; refuses if one already has *this* path open; first run in a fresh worktree does a one-time cold import).
- **Check formatting**: run `Tools/check-format.sh` to verify C# matches house style. It runs **`dotnet format`** (built into the .NET SDK — no extra tooling), driven by the repo `.editorconfig`, so the CLI and Rider apply the same Roslyn rules. `dotnet format` operates on a solution, and the Unity `.sln`/`.csproj` are gitignored/regenerated, so the script **boots Unity** in batch mode to (re)generate them via Unity's built-in `UnityEditor.SyncVS.SyncSolution` (`-executeMethod`, no custom editor script) when missing or stale, then runs `dotnet format` against `Assembler.sln`. This makes it the **heaviest** Tools script (a Unity boot + an MSBuild workspace load — a couple of minutes; Unity emits benign workspace warnings). It **normalises whitespace/indentation and enforces always-braces** (the `:warning` rules) but does **not** re-wrap lines, so your line breaks are preserved. **Check mode (default)** uses `dotnet format --verify-no-changes` (read-only); **`--fix`** writes. **Default** scope is the `.cs` changed vs `master`; pass `--all` for everything under `Assets/`, explicit paths to scope it, or `--fix`. Vendored code (`Assets/Plugins`, `Assets/TextMesh Pro`) is excluded. Exits non-zero if anything needs reformatting.
- **Run tests**: run `Tools/run-tests.sh` to execute the EditMode test suites headlessly (boots Unity in batch mode and invokes the same tests as Window > General > Test Runner, via `Editor.TestBatch.RunEditModeTests` — no UI needed, so Claude can run and verify it). It prints a pass/fail summary and exits non-zero on failure; full NUnit XML lands in `TestResults/EditMode-results.xml`. Pass assembly names to scope the run (`Tools/run-tests.sh Tests.Compiler`), or `--filter <regex>` / `--category <name>`. The in-editor Window > General > Test Runner still works too. Test assemblies live in `Assets/Tests/` per area (`Tests.Compiler`, `Tests.Parsing`, `Tests.Behaviours`, `Tests.Generation`, `Tests.Voxels`, `Tests.Input`, `Tests.Resolving`).
- **Generate behaviour/library docs**: run `Tools/generate-docs.sh` to regenerate both `Assets/docs/Behaviours.md` and `Assets/docs/Libraries.md` headlessly (boots Unity in batch mode and invokes the same code as the editor menus — no UI needed, so Claude can run and verify it). The in-editor menu items `Assembler > Generate Behaviour Docs` / `Generate Library Docs` still work too. This runs fine **concurrently with an editor open on a different path** (e.g. your main checkout), so a branch's docs can be generated in its worktree without checking the branch out — the script refuses only if an editor already has *this* path open. The first run in a fresh worktree does a one-time cold import (~3 min); pass `SEED_LIBRARY=1` to instead clone the main worktree's `Library/` first (only faster when the editor is idle — see the script header).
- **Validate descriptor YAML**: run `Tools/validate-yaml.sh` for a basic structural sanity check on game descriptor YAML (well-formedness + duplicate-key detection) — run it after editing or generating a descriptor to catch syntax errors. It boots Unity in batch mode and invokes `Editor.YamlValidatorBatch.Validate`, reporting each problem with line/column and a source snippet and exiting non-zero if anything is invalid. With no arguments it validates everything in `Assets/ExampleGameDescriptors/`; pass file/dir paths to scope it. It validates YAML *structure* only, not the descriptor schema. The validation itself lives in the **runtime** `Assembler.Validation` assembly (`YamlStructureValidator.Validate(string)` → `YamlValidationResult`), so the engine can validate a descriptor at runtime in a player build on any platform — the script and the `Assembler > Validate Descriptor YAML` editor menu item are just front-ends for the same code.
- **Validate a game boots** (deeper check): run `Tools/validate-game.sh` to confirm a descriptor doesn't just parse but actually *builds a runnable game*. It boots Unity in batch mode and invokes `Editor.GameSandboxValidatorBatch.Validate`, which runs each descriptor through the full load pipeline in a throwaway sandbox — **structure → deserialise → parse → resolve → instantiate** — and reports the outcome **per stage**, so a failure pinpoints the exact stage (and shows the thrown exception / `Debug.LogError`). This catches what structural validation can't: unknown behaviours, bad expressions, missing assets, unbound controls, instantiation errors, no game-over path, etc. The sandbox destroys everything it instantiates after each file, so a whole directory validates in one run. It checks the game *starts* error-free (behaviour `Awake`/`OnEnable` + initialisation wiring run) but does **not** run the per-frame game loop. With no arguments it sandbox-builds everything in `Assets/ExampleGameDescriptors/`; pass file/dir paths to scope it, and it exits non-zero if any file fails. The orchestration lives in `Assembler.Generation.Verification` (`SandboxValidator.Validate(string)` → `SandboxValidationResult`), reusing `Builder.Resolve`/`Builder.Instantiate`; the generation fix-loop (`BuildHarness`) and the `Assembler > Validate Game (sandbox build)` menu item share the same code.

## Architecture

### Pipeline Stages

YAML → **Deserialisation** (DTOs) → **Parsing/Transformation** (Info types) → **Resolving** (IValueProviders) → **Building** (GameObjects) → **Execution**

Each stage has its own assembly (`.asmdef`) and namespace under `Assembler.*`.

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
| `Assembler.Extensions` | `Assembler.Extensions` | Shared extension methods (`VectorExtensions`, `EnumerableExtensions`, `GameObjectExtensions`, `StringExtensions`) |
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

All behaviours are registered in `BehaviourRegistry.All` (`Assets/Parsing/BehaviourRegistry.cs`) as a dictionary mapping string names (e.g. `"velocity"`, `"key hold trigger"`) to factory functions and property descriptors. Each behaviour type consists of:

- An **Info record** (e.g., `VelocityInfo`) in `Assets/Parsing/Info/Behaviours/` — created by the registry factory, holds `ValueSource<T>` properties
- A **Data class** (e.g., `VelocityData`) in `Assets/Resolving/Behaviours/` — holds `IValueProvider<T>` properties for runtime, extends `BehaviourData`
- A **MonoBehaviour** (e.g., `Velocity`) in `Assets/Behaviours/` — extends `GameBehaviour<TData>`, the actual Unity component

Behaviours communicate via a listener/observer pattern: triggers notify downstream behaviours through `Action` delegates. `ListenerInfo` has three variants (`Assets/Parsing/Info/ListenerInfo.cs`):

- `DirectListenerInfo` — targets a specific behaviour by `BehaviourDescriptor` (entity ID + behaviour ID)
- `EntityTaggedListenerInfo` — targets behaviours on any entity matching an entity tag (optionally filtered by behaviour ID)
- `BehaviourTaggedListenerInfo` — targets any behaviour matching a behaviour tag, regardless of entity

Tag values are `ValueSource<string>`, so they can be constants, references, or expressions resolved at runtime. See `Assets/ExampleGameDescriptors/TaggedListenerDemo.yaml` for example usage.

### UI System (composable uGUI blocks)

UI is built from regular behaviours under `Assets/Behaviours/UI/` (`Assembler.Behaviours.UI`), composed via the entity hierarchy — there is no separate UI assembly and the old IMGUI/`ScreenRect` widgets have been removed. The blocks:

- **`ui canvas`** — roots a UI tree with a screen-space `Canvas` + `CanvasScaler` (ScaleWithScreenSize) + `GraphicRaycaster`. Child UI entities compose the interface.
- **`ui container`** — groups child UI entities, auto-laying them out with a vertical/horizontal uGUI layout group (or `Direction: none` for manual positioning), driven by `PreferredWidth`/`PreferredHeight`.
- **`text label`** — a TextMeshPro label; `Text` is re-read every frame, so binding it to a variable/expression shows live values.
- **`ui button`** — a clickable button that acts as a trigger (`NotifyListeners` on click); `Label` is re-read each frame.
- **`ui slider`** — a slider that acts as a trigger, emitting a `value` output whenever it changes.

Prefabs come from a `UiPrefabLibrary` ScriptableObject loaded from `Resources/UI/UiPrefabLibrary`, with typed view components (`UiButtonView`/`UiLabelView`/`UiSliderView` in `UI/Views/`). The `Assembler > UI > Generate UI Prefabs` editor menu regenerates baseline prefabs; restyle them without code changes. `Builder` bootstraps a single `EventSystem` with `InputSystemUIInputModule` (the project is Input System-only) and threads the loaded library through `BehaviourBuildContext`. `GameEntityFactory` pins child sibling order to descriptor order for deterministic layout. See `UiDemo.yaml` and `UiShowcase.yaml`.

### Two-Phase Initialization

Building uses deferred initialization. `GameBehaviourFactory.Create()` returns a tuple of `(GameBehaviour, InitialiseBehaviourEvent)`. All behaviours for all entities are created first, then the `InitialiseBehaviourEvent` delegates are executed afterwards via `InitialisationQueue.ExecuteAll()`. This is necessary because listeners reference other behaviours by `BehaviourDescriptor` (entity ID + behaviour ID), which must all be registered in the `BehaviourRegistry` before wiring.

### Adding a New Behaviour

1. Create an `*Info` record in `Assets/Parsing/Info/Behaviours/` inheriting `BehaviourInfo` with a static `Create` factory method
2. Register in `BehaviourRegistry.All` (`Assets/Parsing/BehaviourRegistry.cs`) with property descriptors
3. Create a `*Data` class in `Assets/Resolving/Behaviours/` inheriting `BehaviourData` (or `TriggerData` for triggers)
4. Create a `GameBehaviour<*Data>` implementation in `Assets/Behaviours/`
5. Add a builder entry in `GameBehaviourFactory.Builders` (`Assets/Building/GameBehaviourFactory.cs`)

### Key Entry Points

- `Builder.Build(yamlPath)` — end-to-end pipeline from a YAML descriptor to a running game (also bootstraps the UI `EventSystem` and prefab library)
- `Transformer.Transform()` — converts `GameDto` → `GameInfo`
- `GameEntityFactory.Create()` — instantiates a single entity with all its behaviours
- `GameBehaviourFactory.Create()` — maps `BehaviourInfo` type to concrete `GameBehaviour` component
- `ValueResolver.Resolve()` — extension method converting `ValueSource<T>` to `IValueProvider<T>`
- `TemplateInstantiator` — expands templates with parameter substitution

### Expression Compiler

`Assets/Compiler/` contains a custom lexer/parser that compiles a C# subset into delegates at runtime. Supports arithmetic, comparison, control flow, method calls, `new` expressions, lambdas, and LINQ. See `Assets/Compiler/COMPILER_SYNTAX_REFERENCE.md` for the full language specification.

### Game Definitions

Example YAML game files are in `Assets/ExampleGameDescriptors/` (e.g. `Pong.yaml`, `Snake 2.yaml`, `Asteroids.yaml`, `Tetris.yaml`, `FlappyBird.yaml`, `TaggedListenerDemo.yaml`, `UiShowcase.yaml`). They define: metadata, assets, constants, variables (including list-typed variables, e.g. `!vec []` for empty vector lists), templates, entities (with behaviours and listeners), and expressions.

IDs for definitions (entities, behaviours, templates, variables, etc.) are promoted to YAML keys at the definition site rather than being a separate `id:` property — i.e. the mapping key *is* the identifier.

## Determinism (Level 1)

Assembler supports **deterministic execution and record/replay**: the same descriptor, seed, and input log reproduce a byte-identical run. This makes generated games debuggable (capture a session and replay it exactly) and testable (play a descriptor through a scripted input log and assert on the outcome).

**The guarantee (Level 1 — same build, same machine):** given a fixed *seed*, *fixed delta time*, and *input log*, a descriptor produces identical execution **on the same build running on the same machine**. This is **NOT cross-platform lockstep** — floating-point and physics results may differ across CPUs, OSes, or Unity versions, so a replay captured on one machine is not guaranteed to reproduce on another.

**Physics caveat:** physics-driven games are **excluded from the determinism guarantee**. Unity's `PhysicsScene` stepping is tied to the engine and not controlled here; manual `Physics.Simulate` is noted as future work. Treat physics as same-machine-best-effort only.

**Controlled nondeterminism sources:**

1. **Time** → `FixedStepGameClock` advances a constant delta per tick (vs. `RealtimeGameClock`'s wall-clock delta).
2. **Randomness** → a single seeded per-run PRNG (`DeterministicRng` / `RandomState`); `RandomMath` routes through it.
3. **Iteration order** → a stable registration index in `BehaviourRegistry`. Queries that would otherwise iterate the unordered `_behaviours` dictionary (`GetByEntityTagAndBehaviourId`) sort by registration order; `GetByBehaviourTag` is already List-backed and stable.
4. **Input** → record/replay at the trigger boundary (`InputTrigger.NotifyListeners` → `TriggerContext`), capturing the ordered set of `(trigger, emitted context)` per logical tick.

**Ordering guarantees:** registration runs in stable entity/behaviour list order, so the registration index is deterministic. Within a single tick, the recorder captures an *ordered* activation list and replay re-injects in recorded order, so downstream behaviour is reproduced regardless of Unity's (unspecified) intra-frame `Update` order among equal-`DefaultExecutionOrder` components.

**Rule — no raw `UnityEngine.Random` in behaviours:** behaviours and libraries must draw randomness through `RandomMath` / `RandomState.Current`, never `UnityEngine.Random` directly. Raw `UnityEngine.Random` reads the engine's global, uncaptured state and breaks replay determinism.

## Workflow

### Committing & Pushing

- **Always commit and push at the end of a session's work** — if a branch exists for the session's work, commit any outstanding changes and push them at the end without asking. Don't wait for the user to request it.

### Addressing PR comments

PR feedback lives in **two separate streams**, and you must fetch **both** — review comments left on a line in the "Files changed" tab do NOT include top-level conversation comments, and it's easy to silently miss a whole category of feedback (as happened once with a "trim stack traces" comment that was only in the conversation stream).

- **Inline review comments** (attached to a file/line): `gh api repos/<owner>/<repo>/pulls/<n>/comments` — this is the "review comments" endpoint.
- **Top-level conversation / issue comments** (not attached to any line): `gh api repos/<owner>/<repo>/issues/<n>/comments` — PRs are issues, so general comments come through the issues endpoint.
- **Review summaries** (the body of an approve/request-changes/comment review): `gh pr view <n> --json reviews`.

Always check all three before declaring PR comments addressed, and enumerate them explicitly so none are dropped. When you finish, reply on the PR mapping each comment to its resolution.

### Git Worktrees

AI work happens in a worktree so the user can keep using the main repo. The user's flow is: work in a worktree → open a PR → check the branch out in the main repo to run it in Unity. To support that, the worktree must be cleaned up once a PR exists and recreated when more work is requested.

- **Before starting work on a branch**: Check whether the branch's worktree exists. If it doesn't (e.g. it was removed after a previous PR), recreate it for that branch before making changes. Never work directly in the main repo for AI changes — the user keeps that checkout for running in Unity.
- **When a PR is created**: After pushing the branch and opening the PR, commit any remaining changes, then remove the local worktree (`git worktree remove`). This frees the branch so the user can `git checkout` it in the main repo and run it in Unity. Tell the user the worktree has been removed and the branch is ready to check out.
- **Follow-up work on the same branch**: If the user requests further changes after the worktree was removed, recreate the worktree for that branch first, then continue working in it. Open a follow-up commit/PR update from the recreated worktree, and remove the worktree again once the PR is updated.
