# Assembler

A Unity 6 framework for defining and running games **declaratively from YAML**. Instead of writing C#, you describe a game as a set of entities composed from reusable behaviours, and a multi-stage pipeline turns that description into a live, playable Unity scene. An LLM can author these descriptors, so the longer-term goal is a mobile app that loads and runs remotely-generated games.

```yaml
Game:
  Title: Simple Pong Game
Variables:                       # runtime-mutable named values: read with !var, written by setter behaviours
  ball velocity: !vec { X: 3, Y: 3 }
  ball radius:   0.2
  paddle spin:   1.5
Entities:
  ball:
    Tags: [ ball ]
    Behaviours:
      sphere gizmo: { Properties: { Radius: !var ball radius, Colour: !colour white } }
      velocity:     { Properties: { Velocity: !var ball velocity } }
  left paddle:
    Behaviours:
      hit trigger:               # a trigger fires; its Listeners name the behaviour that runs in response
        Type: collision enter trigger
        Properties: { TagsToDetect: [ ball ] }
        Listeners:
          - { EntityId: left paddle, BehaviourId: bounce ball }
      bounce ball:               # the listener: writes a new ball velocity computed by an expression
        Type: vector variable setter
        Properties:
          VariableId: !var ball velocity
          Value: !expr           # reflect horizontally, add spin, clamp the vertical speed
            Do: 'new Vector3(-arg0.x * 1.05f, Clamp(arg0.y + arg1, -8f, 8f), 0f)'
            ArgumentTypes: [ vector, float ]
            RegisterTypes:  [ UnityEngine.Vector3 ]
            With: [ !var ball velocity, !var paddle spin ]
      # ...colliders, walls, score listeners
```

A descriptor declares metadata, world/physics settings, assets, constants, variables, templates, and **entities** (each a bag of behaviours). Behaviours talk to each other through a trigger/listener pattern. See [`Assembler/Assets/ExampleGameDescriptors/`](Assembler/Assets/ExampleGameDescriptors/) for ~40 working games (Pong, Snake, Asteroids, Tetris, Pacman, Flappy Bird, 3D demos, UI showcases…).

## How it works

YAML flows through one assembly per stage, transforming representation at each step:

```
YAML
  → Deserialisation   (YamlDotNet → DTOs)
  → Parsing           (DTOs → validated, immutable Info records)
  → Resolving         (ValueSource<T> → IValueProvider<T>)
  → Building          (Info → Unity GameObjects + components)
  → Execution         (the running game)
```

**Three-layer type system.** Every value moves through three forms: a raw **DTO** (`ValueDto`), a validated **Info** source (`ConstantSource<T>`, `ExpressionSource<T>`, `ValueReferenceSource<T>`, …), and a runtime **provider** (`IValueProvider<T>`) that supplies the value during play. This lets a property be a literal, a reference to a variable, or a live expression — uniformly.

**Behaviours** are the unit of composition. Each behaviour type is five pieces: an `*Info` record (parsed config), a `*Data` class (resolved runtime providers), a `GameBehaviour<TData>` MonoBehaviour (the actual component), a `BehaviourRegistry` entry (name → factory + property schema), and a `GameBehaviourFactory` builder. Behaviours communicate via triggers that `NotifyListeners`, with listeners targeted directly, by entity tag, or by behaviour tag.

**Two-phase init.** All behaviours for all entities are created first, then wired — because listeners reference other behaviours by ID, every behaviour must exist before wiring runs.

**Expression compiler.** A custom lexer/parser ([`Assets/Compiler/`](Assembler/Assets/Compiler/)) compiles a C# subset (arithmetic, control flow, method calls, lambdas, LINQ) into delegates at runtime, so descriptors can embed real logic via `!expr`. Static helper libraries (`VectorMath`, `GridMath`, `RandomMath`, …) are callable from expressions.

**LLM generation.** `Assembler.Generation` builds a system prompt from the behaviour catalogue and drives the Anthropic API to author descriptors; `Assembler.Generation.Verification` runs a generate → build → verify loop that retries until a descriptor builds cleanly.

### Assemblies

| Assembly | Purpose |
|---|---|
| `Assembler.Deserialisation` | YAML → DTOs (YamlDotNet) |
| `Assembler.Parsing` | DTOs → validated Info records; behaviour catalogue |
| `Assembler.Compiler` | Runtime C#-subset expression compiler |
| `Assembler.Resolving` | `ValueSource<T>` → `IValueProvider<T>` |
| `Assembler.Building` | Orchestrates the pipeline; `Builder.cs` is the entry point |
| `Assembler.Core` | `GameEntity` / `GameBehaviour<TData>` base types |
| `Assembler.Behaviours` | Concrete behaviours (movement, physics, triggers, UI) |
| `Assembler.Input` | Input System wiring, controls/platform handling |
| `Assembler.Time` | `IGameClock` clock abstraction |
| `Assembler.Libraries` | Static helpers callable from expressions |
| `Assembler.Validation` | Runtime YAML structure validator (platform-agnostic) |
| `Assembler.Generation[.Verification]` | LLM descriptor generation + build/verify loop |
| `Assembler.Voxels` / `Anthropic` / `Extensions` | Voxel format I/O, Anthropic client, shared utilities |

## Running & developing

- **Run a game:** menu `Assembler > Game Launcher` — discovers every descriptor in `ExampleGameDescriptors/`, lets you pick one, and enters Play mode via `Builder.Build(yamlPath)`.

Headless helper scripts (each boots Unity in batch mode — slow; use sparingly):

| Script | Checks |
|---|---|
| `Tools/check-expression.sh` | Expressions compile (cheapest) |
| `Tools/validate-yaml.sh` | Descriptor YAML is structurally well-formed |
| `Tools/validate-game.sh` | A descriptor builds a runnable game (per-stage report) |
| `Tools/check-compile.sh` | Project C# compiles (errors + warnings) |
| `Tools/check-format.sh` | C# matches house style (`dotnet format`) |
| `Tools/run-tests.sh` | EditMode test suites (`Tests.*` assemblies) |
| `Tools/generate-docs.sh` / `check-docs.sh` | Regenerate / verify behaviour & library docs |

## Documentation

- [`Assembler/CLAUDE.md`](Assembler/CLAUDE.md) — full architecture, conventions, and contributor guide
- [`Assets/docs/GameDescriptorSchema.md`](Assembler/Assets/docs/GameDescriptorSchema.md) — descriptor schema
- [`Assets/docs/Behaviours.md`](Assembler/Assets/docs/Behaviours.md) / [`Libraries.md`](Assembler/Assets/docs/Libraries.md) — generated catalogues
- [`Assets/Compiler/COMPILER_SYNTAX_REFERENCE.md`](Assembler/Assets/Compiler/COMPILER_SYNTAX_REFERENCE.md) — expression language
