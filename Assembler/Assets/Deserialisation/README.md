# Assembler.Deserialisation

First stage of the YAML-to-game pipeline. Parses game-descriptor YAML strings into raw DTO objects using YamlDotNet. The downstream `Assembler.Parsing` assembly transforms these DTOs into validated, strongly-typed Info records.

## Public API

| Type / Member | Purpose |
|---|---|
| `GameFileParser.Parse(string yaml)` | Sole entry point. Returns the root `GameDto`. |
| `GameDto` | Root DTO: `InfoDto`, `WorldDto`, `PhysicsDto`, assets, constants, variables, expressions, templates, entities, game-over condition. |
| `EntityDto` / `BehaviourDto` / `ListenerDto` | Entity hierarchy DTOs. |
| `ExpressionDto` | Named expression with an argument list. |
| `VecDto` / `ColourDto` / `VarRefDto` / `ExprRefDto` / `ParamRefDto` / `AssetRefDto` / `OutputRefDto` / `EntityPositionRefDto` / `RefDto` / `TemplateRefDto` | Ref/value DTOs mirroring the YAML schema. |

## Gotchas

- **YamlDotNet** is the only external dependency. Adding a new value type requires a new tag mapping, a new `IYamlTypeConverter`, and a corresponding DTO — all configured inside `GameFileParser`.
- **`ObjectNodeDeserializer`** runs before the built-in `TypeConverterNodeDeserializer` and handles polymorphic `object`-typed properties (e.g. behaviour `Properties`, `Constants`, `Variables`). Dispatches tagged nodes (`!int`, `!float`, `!bool`, `!string`, `!vec`, `!colour`, `!var`, `!expr`); falls back to auto-parsed primitives or `Dictionary<string, object>`. Untagged scalars are inferred as `int`, `float`, `bool`, `string` in that order.
- **Typed list sequences** can carry a type tag (e.g. `!vec []`, `!float []`) and are deserialised as `List<VecDto>`, `List<float>` etc. rather than `List<object>`.
- All DTO properties are nullable by design (any field may be absent in YAML).
- Consumed by `Transformer` in `Assembler.Parsing` (via `Builder.TestBuild()` in `Assembler.Building`).
