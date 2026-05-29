# Assembler.Building

The final stage of the YAML-to-game pipeline. Takes validated `GameInfo` (produced by `Assembler.Parsing`) and instantiates the Unity scene: creates a `GameObject` per entity, attaches `GameEntity` and the concrete `GameBehaviour` components, resolves all `ValueSource<T>` properties into runtime `IValueProvider<T>` instances, and wires up listeners.

`Builder` is the static entry point. `GameEntityFactory` builds the entity tree and implements `IEntitySpawner` for runtime template spawning. `GameBehaviourFactory` maps each `BehaviourInfo` subtype to its concrete MonoBehaviour via the `Builders` dictionary. `BehaviourRegistry` is the runtime lookup of live behaviours by descriptor (and by tag), and `InitialisationQueue` enforces the two-phase init contract: all behaviours are created and registered before any initialisation delegate runs, so listeners can resolve their targets safely.
