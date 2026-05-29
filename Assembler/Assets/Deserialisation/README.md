# Assembler.Deserialisation

The first stage of the YAML-to-game pipeline. Parses game-descriptor YAML strings into raw DTO objects using YamlDotNet. `GameFileParser.Parse(string yaml)` is the sole entry point and returns a `GameDto` root containing entities, behaviours, listeners, expressions, variables, assets, templates, and world/physics settings.

The `Dtos/` subdirectory holds plain `sealed record` types mirroring the YAML schema. `ObjectNodeDeserializer` handles polymorphic `object`-typed properties by dispatching tagged nodes (`!int`, `!float`, `!bool`, `!string`, `!vec`, `!colour`, `!var`, `!expr`); custom `IYamlTypeConverter` implementations handle each tagged value type. Adding a new tag means registering it in `GameFileParser` along with a matching DTO and converter. The downstream `Assembler.Parsing` assembly transforms these DTOs into validated, strongly-typed `Info` records.
