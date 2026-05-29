# Assembler.Building

The final stage of the YAML-to-game pipeline. Takes validated `GameInfo` (produced by `Assembler.Parsing`) and instantiates the Unity scene: GameObjects, MonoBehaviour components, resolved value providers, and wired listeners.

## Public API

| Type / Member | Purpose |
|---|---|
| `Builder.Build(GameInfo)` | Static entry point. Runs the full build sequence. Editor menu items (`Test > Build *`) drive it from YAML files. |
| `GameEntityFactory` | Creates a `GameObject` per entity, attaches a `GameEntity` component, recurses into children, and delegates behaviour creation to `GameBehaviourFactory`. Also implements `IEntitySpawner` for runtime spawning from templates. |
| `GameBehaviourFactory` | Static factory. Maps every `BehaviourInfo` subtype to its concrete `GameBehaviour` MonoBehaviour via an internal `Builders` dictionary. Returns `(GameBehaviour, InitialiseBehaviourEvent)`. Exposes `MonoBehaviourByInfo` for editor doc generation. |
| `BehaviourRegistry` | Runtime lookup: `BehaviourDescriptor → GameBehaviour`. Supports tag-based queries (`GetByBehaviourTag`, `GetByEntityTagAndBehaviourId`) used by tagged listeners. |
| `InitialisationQueue.ExecuteAll(registry)` | Collects deferred `InitialiseBehaviourEvent` delegates from every entity build, then fires them in one pass. |
| `BehaviourBuildContext` | Value-object passed to each builder: `ResolutionContext`, `IEntitySpawner`, `ExclusiveGroupRegistry`. |
| `EntityBuildResult` | Return value of `GameEntityFactory.Create` — flat list of `(Descriptor, Behaviour, Tags)` tuples plus deferred init events. |

## Gotchas

- **Two-phase init is mandatory**: all entities/behaviours must be registered in `BehaviourRegistry` before any `InitialiseBehaviourEvent` runs. `InitialisationQueue.ExecuteAll` enforces this.
- **Adding a new behaviour requires a `Builders` entry**: `GameBehaviourFactory.Create` throws `ArgumentException` for any `BehaviourInfo` type not in the dictionary. Follow the 5-file pattern in `CLAUDE.md`.
- **Name collision with `Assembler.Parsing.BehaviourRegistry`**: the parsing one is a static catalogue; this one is the runtime instance registry. Be explicit with namespaces in `Builder.cs`.
- **Runtime spawning bypasses the queue**: `GameEntityFactory.Spawn` instantiates from a template and fires init events inline (the registry is already populated).
