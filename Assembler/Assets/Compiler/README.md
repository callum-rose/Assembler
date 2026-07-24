# Compiler

A runtime compiler that turns short C# expression strings (written inside YAML game descriptors) into callable delegates. It lexes and parses a procedural C# subset — arithmetic, comparisons, logical operators, control flow, `var`, `new`, lambdas, and LINQ — into expression trees and compiles them to delegates at runtime.

The supported language and its limits are documented in the syntax reference alongside the code; consult it before writing expressions. Expressions appear as a value type in the parsing layer and are compiled to delegates by the resolving layer at the point where they are first read.

## Binding an expression as a descriptor value (`!expr`)

The YAML `!expr` tag is deserialised by `ExprTypeConverter`, which **requires a mapping with a `Do:` key**. The block-scalar form `Waypoints: !expr | <body>` fails at the *deserialise* stage (`Expected 'MappingStart', got 'Scalar'`). Valid forms:

- **Inline body:** `!expr { Do: 'return a + 1;', With: [ !var a ], ArgumentTypes: [ int ] }`
- **Named expression** (declared in the top-level `Expressions:` block, referenced by name): `!expr { Do: route points }` (no `With` for a no-arg expression).

**Return-type / argument-type strings** for the `Expressions:` block and value sources: scalars are `vector`, `int`, `float`, `bool`, etc.; a `List<Vector3>` is **`vector list`**. The compiler has no collection initializers, so build point sequences with the `PositionList` helper:

```csharp
var b = new PositionList();
b.Add(new UnityEngine.Vector3(...));
return b.ToList();
```

See `Pacman.yaml` (`pill field`) and `PatrolDemo.yaml` (`route points`).

### Inline `!expr` can't infer a template `self_id` arg's type — use a named expression

An inline `!expr { Do: '…', With: [...] }` with no `ArgumentTypes` infers each arg's type from its `With` value. But `!entity { Id: !parameter self_id, Property: Position }` has **no resolvable id at parse time** (template parsing runs before `TemplateInstantiator` substitutes `self_id`), so its type can't be inferred and defaults to `Single` — the expr then throws at the *parse* stage: `!entity '' property 'Position' resolves to Vector3 but was used where a Single was expected`.

**Fix:** when a template's inline expr needs the entity's own position/velocity via `!entity { Id: !parameter self_id }`, define a **named** expression in the `Expressions:` block with explicit `ArgumentTypes` (e.g. `[ vector, vector list, float, float ]`) and reference it with `!expr { Do: <name>, With: [...] }`. The explicit types pin everything so no inference on the unsubstituted self-ref is needed (the `PerceptionDemo` `pick goal` pattern). This passes `validate-yaml` (the YAML is structurally fine) but fails `validate-game` at the `parse` stage, so verify there.
