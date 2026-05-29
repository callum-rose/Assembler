# Deserialisation

The first stage of the YAML-to-game pipeline. Parses game-descriptor YAML strings into raw DTO objects, with one DTO type per concept in the schema (entities, behaviours, listeners, expressions, variables, assets, templates, and world/physics settings).

Polymorphic value positions in the schema are supported via custom YAML tags (for things like ints, floats, vectors, colours, variable references, and expression references); each tag is paired with a converter and a DTO so the raw YAML can be dispatched into a strongly-typed shape. The output of this stage is consumed by the next stage of the pipeline, which validates and transforms it.
