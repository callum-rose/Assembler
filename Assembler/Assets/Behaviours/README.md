# Assembler.Behaviours

Concrete `GameBehaviour<TData>` MonoBehaviour implementations — the runtime Unity components for entities defined in YAML game descriptors. Every behaviour type follows the 5-file pattern: `*Info` (parsing) + `*Data` (resolving) + `GameBehaviour<TData>` (here) + a builder entry in `Assembler.Building.GameBehaviourFactory.Builders`.

## Public API

| Type / Member | Purpose |
|---|---|
| `GameBehaviour` | Abstract base. `Execute()` is the entry point called by listeners; `Tags` used for tag-based targeting. |
| `GameBehaviour<TData>` | Typed generic. `Initialise(TData, IReadOnlyList<Listener>)` called during build. |
| `Trigger<T>` (`Triggers/Trigger.cs`) | Base for all triggers; holds a `TriggerContext` for output value propagation. |
| `Listener` / `DirectListener` / `EntityTaggedListener` / `BehaviourTaggedListener` | Observer wiring; `Notify()` applies output mappings then calls `Execute()` on targets. |

Subdirectories group concrete behaviours by category: `Movement/`, `Rotation/`, `Physics/`, `Triggers/{Input,Timing,Physical,Conditionals}/`, `ListOperations/`, `VariableUpdaters/`, `Spawners/`, `Animations/`, `Audio/`, `Camera/`, `Sprites/`, `Visual/`.

## Gotchas

- **Two-phase initialisation**: all behaviours are created first, then `InitialiseBehaviourEvent` delegates run via `InitialisationQueue.ExecuteAll()`. Do not reference other behaviours before initialisation completes.
- Triggers that expose output values must implement `INeedsTriggerContext` to receive a `TriggerContext` during wiring.
- `ListLoopTrigger<T>` (and its typed subclasses) iterate a list variable and fire once per element each cycle.
- Nullable reference types are enforced project-wide; new behaviours must use correct annotations.
