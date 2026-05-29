# Parsing

The second stage of the YAML-to-game pipeline. Transforms the raw DTOs produced by the deserialisation stage into a validated, immutable game model used by everything downstream. Validation, normalisation, and template expansion happen here.

Every property value is wrapped in a deferred "value source" — covering constants, references to variables, expressions, asset lookups, trigger outputs, entity positions, template parameters, and explicit absence — so that resolution to concrete runtime values is delayed until later in the pipeline. This stage also owns the static catalogue of all known behaviour types, mapping each YAML name to the factory and property descriptors that build its info record. Adding a new behaviour starts here.
