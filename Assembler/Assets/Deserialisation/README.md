# Deserialisation

The first stage of the YAML-to-game pipeline. Parses game-descriptor YAML strings into raw DTO objects, with one DTO type per concept in the schema (entities, behaviours, listeners, expressions, variables, assets, templates, and world/physics settings).

Polymorphic value positions in the schema are supported via custom YAML tags (for things like ints, floats, vectors, colours, variable references, and expression references); each tag is paired with a converter and a DTO so the raw YAML can be dispatched into a strongly-typed shape. The output of this stage is consumed by the next stage of the pipeline, which validates and transforms it.

## Descriptor-authoring gotchas

### Sections are mappings, not lists (`key-is-the-id`)

`GameDto` types every section (`Constants`/`Variables`/`Expressions`/`Templates`/`Entities`) as a `Dictionary<…>`, so definitions use the **key-is-the-id mapping** form — `X: { … }` for definitions and `B:\n  Type: T` for behaviours. The legacy **list style** (`- Id: X`, behaviours as `- Type: T, Id: B`) no longer deserialises: it fails at this stage with `YamlException: Expected 'MappingStart', got 'SequenceStart'`, and because everything goes through the one `GameFileParser`, such a file can't load *or* be validated (`validate-game` reports `FAIL deserialise`, `check-expression` reports `SKIP`). If a descriptor mysteriously fails at deserialise on a line inside one of those blocks, check for a stray `- Id:` and convert to mapping form.

### Template-authoring gotchas

Found while authoring template-heavy descriptors:

- **Enum-typed behaviour properties can't be parameterised.** `Shape: !parameter shape` fails at parse (`Cannot convert StringValue to UnityEngine.PrimitiveType`). Keep the mechanics in the template and layer per-entity *visual* behaviours on top of the `Template:` ref instead.
- **`state machine` `OnEnter`/`OnExit` hooks reject the bare-scalar listener shorthand** (`entries must be listener maps`). Inside a template, spell them out — `{ EntityId: !parameter self_id, BehaviourId: … }` — even though a plain `Listeners:` accepts bare scalars with a self-defaulting `EntityId`.
- **Template per-entity `Variables` aren't type-inferred at inline `!expr` call sites**, even when seeded from typed literals like `!vec`. Add `ArgumentTypes:` hints (same failure class as the inline-`!expr` self-id issue documented in `Assets/Compiler/README.md`).
