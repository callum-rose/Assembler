# Behaviours

Concrete MonoBehaviour implementations — the runtime Unity components for entities defined in YAML game descriptors. Each behaviour is described declaratively at parse time, has its values resolved at build time, and runs here as a MonoBehaviour attached to its entity.

Subfolders group behaviours by purpose: movement, rotation, physics, spawners, list operations, variable updaters, animations, audio, camera, sprites, visuals, and the various trigger families (input, timing, physical contact, conditionals). Also contains the abstract base types for behaviours and triggers, and the listener types used to wire triggers to the downstream behaviours they fire.
