# Parsing

The second stage of the YAML-to-game pipeline. Transforms the raw DTOs produced by the deserialisation stage into a validated, immutable model of the game — a tree of info records describing the world, entities, behaviours, triggers, listeners, templates, variables, expressions, and assets. Validation, normalisation, and template expansion all happen here.

Every property value is wrapped in a deferred "value source" — covering constants, references to variables, expressions, asset lookups, trigger outputs, entity positions, template parameters, and explicit absence — so that resolution to concrete runtime values is delayed until later in the pipeline. This stage also owns the static catalogue of all known behaviour types, mapping each YAML name to the factory and property descriptors that build its info record. Adding a new behaviour starts with a new info record here.

## Expressions: the `!expr { Do, With }` call site

Every expression call site in a descriptor uses one uniform form:

```yaml
Value: !expr { Do: <name-or-body>, With: [ <operand>, ... ] }
```

`Do` is dispatched deterministically by registry membership (**name wins**):

- **Named call** — if `Do` matches a declared expression (by its id or its `CallableAs`
  alias in the top-level `Expressions:` block), it's a call into that expression. `With`
  binds positionally to the expression's declared parameters, typed by its `ArgumentTypes`.

  ```yaml
  Expressions:
    int add:
      ArgumentTypes: [ int, int ]
      ArgumentNames: [ a, b ]
      ReturnType: int
      Expression: 'return a + b;'
  # …
  Value: !expr { Do: int add, With: [ !var score, 1 ] }
  ```

- **Inline anonymous body** — otherwise `Do` is compiled as a one-off C# body (full
  [compiler syntax](../Compiler/COMPILER_SYNTAX_REFERENCE.md), so precedence and multiple
  operators work for free). `With` binds positionally to params `arg0`, `arg1`, `arg2`, …:

  ```yaml
  Position: !expr { Do: '-arg0',      With: [ !var velocity ] }
  Position: !expr { Do: 'arg0 * 2',   With: [ !var velocity ] }
  Health:   !expr { Do: 'arg0 + arg1', With: [ !var hp, !var bonus ] }
  ```

  Operand types are inferred (constants by literal kind, `!var` by its resolved value,
  a nested named `!expr` by its return type); the return type is the use-site type. Each
  inline body becomes an anonymous expression (`__inline_N`) compiled alongside the
  declared ones. A bare expression body gets an implicit `return … ;`; a body containing
  `;` is passed through as hand-written statements.

Variable operands stay explicit `!var foo` tags inside `With`; everything resolves through
the same `ValueSource<T>` → `IValueProvider<T>` pipeline as any other value.
