# Deserialisation

First stage of the Assembler pipeline (`Assembler.Deserialisation` assembly). Parses game-descriptor YAML strings into raw DTO objects using YamlDotNet. The downstream `Assembler.Parsing` assembly transforms these DTOs into validated, strongly-typed Info records.

## Public API

**`GameFileParser.Parse(string yaml) → GameDto`**
The sole entry point. Call this with the raw YAML text of a game descriptor; it returns the root `GameDto`.

**DTOs (`Dtos/`)**
Plain `sealed record` types mirroring the YAML schema:
- `GameDto` — root; contains `InfoDto`, `WorldDto`, `PhysicsDto`, assets, constants, variables, expressions, templates, entities, and game-over condition.
- `EntityDto` / `BehaviourDto` / `ListenerDto` — entity hierarchy.
- `ExpressionDto` — named expression with an argument list.
- Ref/value DTOs: `VecDto`, `ColourDto`, `VarRefDto`, `ExprRefDto`, `ParamRefDto`, `AssetRefDto`, `OutputRefDto`, `EntityPositionRefDto`, `RefDto`, `TemplateRefDto`.

## Gotchas and Dependencies

- **YamlDotNet** is the only external dependency. Tag mappings and custom converters are configured once inside `GameFileParser`; adding a new value type requires a new tag mapping, a new `IYamlTypeConverter`, and a corresponding DTO.
- **`ObjectNodeDeserializer`** runs before the built-in `TypeConverterNodeDeserializer` and handles polymorphic `object`-typed properties (e.g. behaviour `Properties`, `Constants`, `Variables`). It dispatches tagged nodes (`!int`, `!float`, `!bool`, `!string`, `!vec`, `!colour`, `!var`, `!expr`) and falls back to auto-parsed primitives or `Dictionary<string, object>`. Untagged scalars are inferred as `int`, `float`, `bool`, or `string` in that order.
- **Typed lists** — sequences can carry a type tag (e.g. `!vec []`, `!float []`) and are deserialized as `List<VecDto>`, `List<float>`, etc. rather than `List<object>`.
- `IsExternalInit.cs` is a compiler shim required for `record` / `init` syntax on the project's target runtime — do not remove it.
- Nullable reference types are enabled via `csc.rsp`; all DTO properties are nullable by design since any field may be absent in YAML.
- `GameFileParser` is consumed by `Transformer` in `Assembler.Parsing` (via `Builder.TestBuild()` in `Assembler.Building`).
