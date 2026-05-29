# Behaviours

Concrete `GameBehaviour<TData>` MonoBehaviour implementations (`Assembler.Behaviours` assembly) — the runtime Unity components for entities defined in YAML game descriptors.

## 5-File Pattern

Every behaviour type spans three layers:

| File | Location | Purpose |
|---|---|---|
| `*Info` record | `Assets/Parsing/Info/Behaviours/` | Holds `ValueSource<T>` properties; created by `BehaviourRegistry.All` factory |
| `*Data` class | `Assets/Resolving/Behaviours/` | Holds resolved `IValueProvider<T>` properties used at runtime |
| `GameBehaviour<TData>` | `Assets/Behaviours/` (here) | Unity MonoBehaviour; calls `Execute()`, notifies listeners |

A builder entry in `GameBehaviourFactory.Builders` (`Assets/Building/`) wires them together.

## Key Public Types

- `GameBehaviour` — abstract base; `Execute()` is the entry point called by listeners; `Tags` used for tag-based targeting
- `GameBehaviour<TData>` — typed generic; `Initialise(TData, IReadOnlyList<Listener>)` called during build
- `Trigger<T>` (`Triggers/Trigger.cs`) — base for all triggers; holds a `TriggerContext` for output value propagation
- `Listener` / `DirectListener` / `EntityTaggedListener` / `BehaviourTaggedListener` — observer wiring; `Notify()` applies output mappings then calls `Execute()` on targets

## Behaviour Categories

- `Movement/` — velocity, translate, set position
- `Rotation/` — angular velocity, rotate, set rotation
- `Physics/` — rigidbody, add force/impulse/torque, collider auto-add, set velocity
- `Triggers/Input/` — keyboard, mouse, gamepad, touch (tap, swipe, pinch, drag, etc.)
- `Triggers/Timing/` — on start, every frame, interval, timer, debounce, throttle, deferred
- `Triggers/Physical/` — collision/trigger enter/exit/stay
- `Triggers/Conditionals/` — condition gate, exclusive trigger
- `ListOperations/` — add/remove/insert/set/clear for all list variable types
- `VariableUpdaters/` — typed setters for bool, float, int, string, vector3
- `Spawners/` — spawn and destroy entities
- `Animations/`, `Audio/`, `Camera/`, `Sprites/`, `Visual/` — rendering and audiovisual

## Gotchas

- Initialisation is two-phase: all behaviours are created first, then `InitialiseBehaviourEvent` delegates run via `InitialisationQueue.ExecuteAll()`. Do not reference other behaviours before initialisation completes.
- Nullable reference types are enforced project-wide; new behaviours must use correct annotations.
- Triggers that expose output values must implement `INeedsTriggerContext` to receive a `TriggerContext` during wiring.
- `ListLoopTrigger<T>` (and its typed subclasses) iterate a list variable and fire once per element each cycle.
