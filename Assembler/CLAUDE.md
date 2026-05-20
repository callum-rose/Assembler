# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Assembler is a Unity 6 (6000.4.5f1) framework for defining and running games declaratively via YAML configuration files. Games are described as entities with composable behaviours, and a multi-stage pipeline transforms YAML into executable Unity GameObjects.

## Build & Test

This is a Unity project — there is no CLI build. Open in Unity Editor 6000.4.5f1.

- **Run game**: Unity Editor menu `Test > Build` (invokes `Builder.TestBuild()` which loads `Assets/GameDescriptors/Pong.yaml`)
- **Run tests**: Unity Editor > Window > General > Test Runner (NUnit framework)
- **Generate behaviour docs**: Unity Editor menu `Assembler > Generate Behaviour Docs`

Tests are in `Assets/Tests/` with separate assemblies per area (`Tests.Compiler`, `Tests.Parsing`).

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

### Three-Layer Type System

1. **DTOs** (`Assets/Deserialisation/Dtos/`): Raw deserialized YAML — `GameDto`, `EntityDto`, `BehaviourDto`, `ValueDto`
2. **Info records** (`Assets/Parsing/Info/`): Validated, immutable records — `GameInfo`, `EntityInfo`, `BehaviourInfo` subclasses. Values are wrapped as `ValueSource<T>` (abstract) with concrete subtypes: `ConstantSource<T>`, `ValueReferenceSource<T>`, `ExpressionSource<T>`, `AssetSource<T>`, `TriggerOutputSource<T>`, `None<T>`
3. **Runtime providers** (`Assets/Resolving/`): `IValueProvider<T>` implementations that supply values during gameplay — `ValueProvider<T>`, `ExpressionValueProvider<T>`, `TriggerOutputProvider<T>`, `NullValueProvider<T>`

### Behaviour System

All behaviours are registered in `BehaviourRegistry.All` (`Assets/Parsing/BehaviourRegistry.cs`) as a dictionary mapping string names (e.g. `"velocity"`, `"key hold trigger"`) to factory functions and property descriptors. Each behaviour type consists of:

- An **Info record** (e.g., `VelocityInfo`) in `Assets/Parsing/Info/Behaviours/` — created by the registry factory, holds `ValueSource<T>` properties
- A **Data class** (e.g., `VelocityData`) in `Assets/Resolving/Behaviours/` — holds `IValueProvider<T>` properties for runtime, extends `BehaviourData`
- A **MonoBehaviour** (e.g., `Velocity`) in `Assets/Behaviours/` — extends `GameBehaviour<TData>`, the actual Unity component

Behaviours communicate via a listener/observer pattern: triggers notify downstream behaviours through `Action` delegates.

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

Example YAML game files (`Pong.yaml`, `Snake.yaml`) are in `Assets/GameDescriptors/`. They define: metadata, assets, constants, variables, templates, entities (with behaviours and listeners), and expressions.

## Workflow

### Git Worktrees

Use worktrees only for initial parallel work when the user is on a different branch. For follow-up work on a branch the user already has checked out, work directly in the main repo.

- **Before starting parallel work**: Check if the required worktree exists and create it if it doesn't.
- **After finishing**: Commit all changes, then delete the worktree so the user can checkout the branch in the main repo to verify in Unity.
- **Follow-up work**: If the user requests more changes and is already on the branch, work directly in the main project directory — no worktree needed.
