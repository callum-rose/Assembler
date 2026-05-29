# Building

The final stage of the YAML-to-game pipeline. Takes the validated, immutable game model produced by the previous stage and instantiates the live Unity scene: creates a GameObject per entity, attaches the concrete behaviour components, resolves every declarative value into a runtime value provider, and wires up listeners between triggers and their targets.

Initialisation runs in two phases — every entity and behaviour is created and registered first, and only then do the per-behaviour initialisation steps run. This ordering is required because listeners reference other behaviours by identity, so the full registry must exist before any wiring can resolve. Runtime spawning of templated entities also lives here, so behaviours can create new entities mid-game.
