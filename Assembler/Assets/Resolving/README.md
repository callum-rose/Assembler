# Assembler.Resolving

The third stage of the pipeline. Converts parsed `ValueSource<T>` instances (from `Assembler.Parsing`) into runtime `IValueProvider<T>` instances that behaviours read during gameplay. Also holds all per-run state registries (variables, assets, compiled expressions, entity transforms).

## Key Types

| Type | Role |
|---|---|
| `IValueProvider<T>` | Core interface — `Value { get; set; }`. Read by behaviour `Update` loops. |
| `ValueResolver.Resolve<T>(ValueSource<T>, ResolutionContext)` | Extension method; the single entry point for the conversion. Dispatches on `ValueSource` subtype. |
| `ResolutionContext` | Immutable record passed to every `Resolve` call; bundles all registries. |
| `ValueProvider<T>` | Wraps a plain mutable value (constants, variables). |
| `ExpressionValueProvider<T>` | Calls a compiled delegate each time `.Value` is read. |
| `TriggerOutputProvider<T>` | Reads a named output from the current `TriggerContext` (set by the firing trigger). |
| `NullValueProvider<T>` | Singleton sentinel for `None<T>` sources — throws on `.Value` read. |
| `VariableRegistry` | Global mutable store of `IValueProvider` instances keyed by variable ID. |
| `EntityVariableScope` | Per-entity locals that shadow globals; disposed after entity initialisation. |
| `AssetRegistry` | Loads Unity assets via `Resources.Load` and looks them up by ID. |
| `CompiledExpressionsRegistry` | Holds pre-compiled expression delegates looked up during resolution. |
| `BehaviourData` | Base class for all per-behaviour resolved data bags (one subclass per behaviour). |
| `TriggerData` | `BehaviourData` subclass for trigger behaviours. |

## Gotchas & Dependencies

- **Depends on** `Assembler.Parsing` (for `ValueSource<T>`, `BehaviourInfo`, `ValueInfo`, etc.) and `Assembler.Compiler` (expressions).
- `TriggerOutputSource<T>` requires a non-null `TriggerContext` in `ResolutionContext`; resolving it outside a trigger callback throws at runtime.
- `int` variables are implicitly widened to `float` by `VariableRegistry.Get<T>` — no other coercions are supported.
- `EntityVariableScope` is `IDisposable`; locals are cleared on disposal to catch stale access.
- `BehaviourData` subclasses in `Behaviours/` are created by `GameBehaviourFactory` (in `Assembler.Building`) and passed to the corresponding `GameBehaviour<TData>` component via two-phase initialisation.
- Only `"resources"` is a supported asset source (`Resources.Load`); other source strings throw `NotImplementedException`.
