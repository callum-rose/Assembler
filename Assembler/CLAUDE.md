# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Assembler is a Unity 6 (6000.4.5f1) framework for defining and running games declaratively via YAML configuration files. Games are described as entities with composable behaviours, and a multi-stage pipeline transforms YAML into executable Unity GameObjects.

## Code Conventions

- **Be concise** in all responses — favour short, direct answers and avoid unnecessary preamble or repetition.
- **Nullable reference types** are enabled project-wide (`Assets/Parsing/csc.rsp`). All new and modified code must respect nullable annotations — use `?` for nullable references, avoid `null!` suppression unless justified, and handle nullability properly.
- **Unity `.meta` files** never need to be created manually — Unity generates them automatically. Don't author or hand-create `.meta` files for new assets or scripts.

## Build & Test

This is a Unity project — there is no CLI build. Open in Unity Editor 6000.4.5f1.

- **Run game**: Unity Editor menu `Test > Build` (invokes `Builder.TestBuild()` which loads `Assets/GameDescriptors/Pong.yaml`)
- **Run tests**: Window > General > Test Runner (NUnit). Test assemblies live in `Assets/Tests/` per area (`Tests.Compiler`, `Tests.Parsing`, `Tests.Behaviours`, `Tests.Generation`, `Tests.Voxels`).
- **Generate behaviour/library docs**: run `Tools/generate-docs.sh` to regenerate both `Assets/docs/Behaviours.md` and `Assets/docs/Libraries.md` headlessly (boots Unity in batch mode and invokes the same code as the editor menus — no UI needed, so Claude can run and verify it). The in-editor menu items `Assembler > Generate Behaviour Docs` / `Generate Library Docs` still work too.

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
| `Assembler.Behaviours` | `Assembler.Behaviours` | Concrete behaviour implementations (movement, physics, triggers, etc.) |
| `Assembler.Voxels` | `Assembler.Voxels` | Goxel `.txt` voxel format parsing/writing and coordinate conversion |
| `Assembler.Anthropic` | `Assembler.Anthropic` | Minimal HTTP client for the Anthropic Messages API |
| `Assembler.Generation` | `Assembler.Generation` | LLM-driven YAML game-descriptor generation; wraps `AnthropicClient` with a system prompt built from the behaviour catalogue |

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

Tag values are `ValueSource<string>`, so they can be constants, references, or expressions resolved at runtime. See `Assets/GameDescriptors/TaggedListenerDemo.yaml` for example usage.

### Two-Phase Initialization

Building uses deferred initialization. `GameBehaviourFactory.Create()` returns a tuple of `(GameBehaviour, InitialiseBehaviourEvent)`. All behaviours for all entities are created first, then the `InitialiseBehaviourEvent` delegates are executed afterwards via `InitialisationQueue.ExecuteAll()`. This is necessary because listeners reference other behaviours by `BehaviourDescriptor` (entity ID + behaviour ID), which must all be registered in the `BehaviourRegistry` before wiring.

### Adding a New Behaviour

1. Create an `*Info` record in `Assets/Parsing/Info/Behaviours/` inheriting `BehaviourInfo` with a static `Create` factory method
2. Register in `BehaviourRegistry.All` (`Assets/Parsing/BehaviourRegistry.cs`) with property descriptors
3. Create a `*Data` class in `Assets/Resolving/Behaviours/` inheriting `BehaviourData` (or `TriggerData` for triggers)
4. Create a `GameBehaviour<*Data>` implementation in `Assets/Behaviours/`
5. Add a builder entry in `GameBehaviourFactory.Builders` (`Assets/Building/GameBehaviourFactory.cs`)

### Key Entry Points

- `Builder.TestBuild()` — end-to-end pipeline from YAML to running game
- `Transformer.Transform()` — converts `GameDto` → `GameInfo`
- `GameEntityFactory.Create()` — instantiates a single entity with all its behaviours
- `GameBehaviourFactory.Create()` — maps `BehaviourInfo` type to concrete `GameBehaviour` component
- `ValueResolver.Resolve()` — extension method converting `ValueSource<T>` to `IValueProvider<T>`
- `TemplateInstantiator` — expands templates with parameter substitution

### Expression Compiler

`Assets/Compiler/` contains a custom lexer/parser that compiles a C# subset into delegates at runtime. Supports arithmetic, comparison, control flow, method calls, `new` expressions, lambdas, and LINQ. See `Assets/Compiler/COMPILER_SYNTAX_REFERENCE.md` for the full language specification.

### Game Definitions

Example YAML game files are in `Assets/GameDescriptors/` (e.g. `Pong.yaml`, `Snake.yaml`, `Snake 2.yaml`, `TaggedListenerDemo.yaml`). They define: metadata, assets, constants, variables (including list-typed variables, e.g. `!vec []` for empty vector lists), templates, entities (with behaviours and listeners), and expressions.

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

### Git Worktrees

AI work happens in a worktree so the user can keep using the main repo. The user's flow is: work in a worktree → open a PR → check the branch out in the main repo to run it in Unity. To support that, the worktree must be cleaned up once a PR exists and recreated when more work is requested.

- **Before starting work on a branch**: Check whether the branch's worktree exists. If it doesn't (e.g. it was removed after a previous PR), recreate it for that branch before making changes. Never work directly in the main repo for AI changes — the user keeps that checkout for running in Unity.
- **When a PR is created**: After pushing the branch and opening the PR, commit any remaining changes, then remove the local worktree (`git worktree remove`). This frees the branch so the user can `git checkout` it in the main repo and run it in Unity. Tell the user the worktree has been removed and the branch is ready to check out.
- **Follow-up work on the same branch**: If the user requests further changes after the worktree was removed, recreate the worktree for that branch first, then continue working in it. Open a follow-up commit/PR update from the recreated worktree, and remove the worktree again once the PR is updated.
