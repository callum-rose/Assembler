# Parsing

The second stage of the YAML-to-game pipeline. Transforms the raw DTOs produced by the deserialisation stage into a validated, immutable model of the game — a tree of info records describing the world, entities, behaviours, triggers, listeners, templates, variables, expressions, and assets. Validation, normalisation, and template expansion all happen here.

Every property value is wrapped in a deferred "value source" — covering constants, references to variables, expressions, asset lookups, trigger outputs, entity positions, template parameters, and explicit absence — so that resolution to concrete runtime values is delayed until later in the pipeline. This stage also owns the static catalogue of all known behaviour types, mapping each YAML name to the factory and property descriptors that build its info record. Adding a new behaviour starts with a new info record here.

## Expressions: the `!expr { Do, With }` call site

Every expression call site uses one form — `!expr { Do: <name-or-body>, With: [ <operand>, … ] }` —
dispatched by registry membership (**name wins**):

- **Named call** — `Do` matches a declared `Expressions:` entry (by id or `CallableAs`); `With` binds
  to its declared parameters: `!expr { Do: int add, With: [ !var score, 1 ] }`.
- **Inline body** — otherwise `Do` is a one-off C# body ([compiler syntax](../Compiler/COMPILER_SYNTAX_REFERENCE.md))
  with `With` bound positionally to `arg0`, `arg1`, …: `!expr { Do: 'arg0 * 2', With: [ !var velocity ] }`.
  Operand and return types are inferred (return type from the use-site); a body without `;` gets an
  implicit `return … ;`. Each becomes an anonymous expression compiled with the declared ones.

Inline bodies may add hints — `ReturnType`, `ArgumentTypes`, `RegisterTypes`, `RegisterTypeStatics` —
for when inference can't reach (e.g. an `object`-typed spawner `Parameters:` slot needs `ReturnType`).
On a named call they're ignored (logged).
