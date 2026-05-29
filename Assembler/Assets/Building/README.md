# Assembler.Building

The final stage of the YAML-to-game pipeline. Takes validated `GameInfo` (produced by `Assembler.Parsing`) and instantiates the Unity scene: GameObjects, MonoBehaviour components, resolved value providers, and wired listeners.

## Key Types

| Type | Role |
|---|---|
| `Builder` | Static entry point. `Build(GameInfo)` runs the full build sequence. Editor menu items (`Test/Build *`) drive it from YAML files. |
| `GameEntityFactory` | Creates a `GameObject` per entity, attaches a `GameEntity` component, recurses into children, and delegates behaviour creation to `GameBehaviourFactory`. Also implements `IEntitySpawner` for runtime spawning from templates. |
| `GameBehaviourFactory` | Static factory. Maps every `BehaviourInfo` subtype to its concrete `GameBehaviour` MonoBehaviour via an internal `Builders` dictionary. Returns `(GameBehaviour, InitialiseBehaviourEvent)` — the event is deferred. Also exposes `MonoBehaviourByInfo` for editor doc generation. |
| `BehaviourRegistry` | Runtime lookup: `BehaviourDescriptor → GameBehaviour`. Supports tag-based queries (`GetByBehaviourTag`, `GetByEntityTagAndBehaviourId`) used by `EntityTaggedListener` and `BehaviourTaggedListener`. |
| `InitialisationQueue` | Collects all deferred `InitialiseBehaviourEvent` delegates from every entity build, then fires them in one pass via `ExecuteAll(registry)`. |
| `BehaviourBuildContext` | Value-object passed to each builder: `ResolutionContext`, `IEntitySpawner`, and `ExclusiveGroupRegistry`. |
| `EntityBuildResult` | Return value of `GameEntityFactory.Create` — the flat list of `(Descriptor, Behaviour, Tags)` tuples plus the deferred init events for that entity tree. |

## Crucial Notes

- **Two-phase init is mandatory.** All entities and behaviours must be created and registered in `BehaviourRegistry` before any `InitialiseBehaviourEvent` runs. Listeners resolve their target behaviours by `BehaviourDescriptor` during init, so the registry must be fully populated first. `InitialisationQueue.ExecuteAll` enforces this.

- **Adding a new behaviour requires a `Builders` entry.** `GameBehaviourFactory.Create` throws `ArgumentException` for any `BehaviourInfo` type not in the `Builders` dictionary. Follow the 5-file pattern in `CLAUDE.md` and add an entry to `CreateBuilders()`.

- **Name collision with `Assembler.Parsing.BehaviourRegistry`.** The parsing assembly has its own `BehaviourRegistry` (static catalogue of behaviour factories). This assembly's `BehaviourRegistry` is the runtime instance mapping descriptors to live components. Both names are in scope in `Builder.cs` — be explicit with namespaces if needed.

- **Runtime spawning bypasses the queue.** `GameEntityFactory.Spawn` instantiates from a named template and immediately fires init events inline (the registry is already populated at that point).
