# Assembler.Behaviours

Concrete `GameBehaviour<TData>` MonoBehaviour implementations — the runtime Unity components for entities defined in YAML game descriptors. Each behaviour follows the 5-file pattern: an `*Info` record in `Assets/Parsing/`, a `*Data` class in `Assets/Resolving/`, the MonoBehaviour here, and a builder entry in `Assembler.Building.GameBehaviourFactory`.

Subdirectories group concrete behaviours by category: `Movement/`, `Rotation/`, `Physics/`, `Triggers/{Input,Timing,Physical,Conditionals}/`, `ListOperations/`, `VariableUpdaters/`, `Spawners/`, `Animations/`, `Audio/`, `Camera/`, `Sprites/`, and `Visual/`. The directory also contains the abstract `GameBehaviour` base, the typed `GameBehaviour<TData>`, the `Trigger<T>` base for trigger behaviours, and the `Listener` family used to wire triggers to downstream behaviours.
