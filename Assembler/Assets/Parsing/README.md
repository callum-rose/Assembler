# Assembler.Parsing

The second stage of the pipeline. Transforms raw DTOs (from `Assembler.Deserialisation`) into validated, strongly-typed `Info` records consumed by `Assembler.Building`.

## Role

- Validates and normalises YAML data into immutable records.
- Wraps all property values in `ValueSource<T>` to defer resolution of variables, expressions, asset refs, and trigger outputs to runtime.
- Maintains the static catalogue of all known behaviour types (`BehaviourRegistry.All`).

## Key Public API

| Type / Member | Purpose |
|---|---|
| `Transformer.Transform(GameDto)` | Entry point — converts a `GameDto` into a `GameInfo` |
| `GameInfo` | Root info record: entities, templates, variables, assets, expressions, world/physics settings |
| `BehaviourInfo` | Abstract base for all per-behaviour info records; holds `Id`, `Listeners`, and `Tags` |
| `ValueSource<T>` | Abstract wrapper for a property value — subtypes: `ConstantSource`, `ValueReferenceSource`, `ExpressionSource`, `AssetSource`, `TriggerOutputSource`, `EntityPositionSource`, `ParameterSource`, `None` |
| `ListenerInfo` | Abstract base for trigger wiring — subtypes: `DirectListenerInfo`, `EntityTaggedListenerInfo`, `BehaviourTaggedListenerInfo` |
| `BehaviourRegistry.All` | `IReadOnlyDictionary<string, BehaviourFactory>` mapping YAML behaviour names to factory functions |
| `TemplateInstantiator.Instantiate(...)` | Merges a template `EntityInfo` with override parameters to produce a `ConcreteEntityInfo` |
| `TransformContext` | Carries variables, parameters, expressions, and a type registry through recursive transformation |

## Gotchas

- **Two `BehaviourRegistry` types exist**: `Assembler.Parsing.BehaviourRegistry` (static catalogue, parse-time) vs `Assembler.Building.BehaviourRegistry` (runtime instance registry). Do not confuse them.
- Every new behaviour requires a matching entry in `BehaviourRegistry.All` and an `*Info` record under `Info/Behaviours/`.
- `GameInfo.ParseContext` is set after construction by `Transformer.Transform` so runtime spawners can re-enter `TemplateInstantiator.Instantiate` with the original context.
- Nullable reference types are enforced project-wide via `csc.rsp`. All code here must respect nullable annotations.
- Downstream consumers: `Assembler.Building` reads `GameInfo`, `Assembler.Resolving` converts `ValueSource<T>` to `IValueProvider<T>`, and `Assembler.Generation` reflects over `BehaviourRegistry.All` to build the LLM system prompt.
