---
name: add-yaml-tag
description: >
  Use this skill whenever the user wants to add a new custom YAML tag (e.g. `!foo`) to the Assembler
  game descriptor format. Trigger on requests like "add a new yaml tag", "I want a `!rect` tag",
  "support `!range` in descriptors", "register a new tag in GameFileParser", or any task that ends
  with a new `!something` appearing inside `Assets/ExampleGameDescriptors/*.yaml`. This skill covers
  the deserialisation layer only — DTO, type converter, and `GameFileParser` registration. It
  intentionally does NOT wire the tag through Phase1/2/3 parsing, Info records, resolvers, or
  builders, because how each tag is consumed downstream varies and must be designed per-tag.
---

# Add YAML Tag

This skill walks you through adding a new custom YAML tag to the Assembler deserialisation layer.

Existing tags include `!vec`, `!colour`, `!var`, `!expr`, `!parameter`, `!asset`, `!output`,
`!entity_position`, `!clock`. All follow the same three-part pattern: **DTO record + IYamlTypeConverter +
registration in GameFileParser**. (`!clock` is a good end-to-end reference for a scalar value-source
tag wired all the way through to a runtime provider: `ClockRefDto` → `ClockRef` → `ClockValueSource<T>`
→ `ClockValueProvider<T>`.)

## Scope

This skill stops once `GameFileParser.Parse(yaml)` returns a `GameDto` whose tree contains your new
DTO. Wiring the DTO through the Parsing → Resolving → Building layers is *out of scope* — each tag
gets consumed differently downstream (some become `ValueSource<T>`, some become parameters on a
specific `*Info` record, some are scalar references resolved at runtime). After this skill, hand
back to the user to design the downstream usage.

If the user is unclear whether they want a tag at all, push back: a new tag is only justified when
a value's *kind* is ambiguous from context, or the value needs special syntax. If the same data
could live in a normal mapping without ambiguity, no tag is needed.

---

## The pattern

Three files. One edit.

```
Assets/Deserialisation/
├── Dtos/<Name>Dto.cs              # NEW — data shape
├── <Name>TypeConverter.cs         # NEW — YAML → DTO parsing
└── GameFileParser.cs              # EDIT — register tag + converter
```

Use `internal` for the converter, `public sealed record` for the DTO. Match the existing files —
they're tiny and consistent, mimic them rather than inventing structure.

---

## Step 1 — Decide the tag shape

Before writing any code, pin down two things and confirm with the user if unclear:

1. **Tag name.** Lowercase, snake_case if multi-word, leading `!`. Examples: `!rect`,
   `!entity_position`. Match the casing convention of existing tags.
2. **YAML form.** Is the tag attached to a **scalar**, a **mapping**, a **sequence**, or *multiple*
   of those? This determines which converter shape to copy.

   | Form         | Example YAML                       | Reference converter            |
   |--------------|------------------------------------|--------------------------------|
   | scalar       | `health: !var max_hp`              | [VarTypeConverter](../../Assets/Deserialisation/VarTypeConverter.cs)        |
   | mapping      | `position: !vec { X: 1, Y: 2 }`    | [VecTypeConverter](../../Assets/Deserialisation/VecTypeConverter.cs) (mapping branch) |
   | scalar OR mapping | `colour: !colour "#ff0000"` *or* `!colour { R: 1, G: 0, B: 0 }` | [ColourTypeConverter](../../Assets/Deserialisation/ColourTypeConverter.cs) |
   | mapping OR sequence (list) | `path: !vec [{X: 0, Y: 0}, {X: 1, Y: 1}]` | [VecTypeConverter](../../Assets/Deserialisation/VecTypeConverter.cs) (full file) |

Whichever shape matches, **read that file first** and use it as the template. You will write
something structurally identical.

---

## Step 2 — Create the DTO

Location: `Assets/Deserialisation/Dtos/<Name>Dto.cs`

Two flavours exist; pick the one that matches your shape.

**Reference DTO** (just an ID string — for `!var`, `!parameter`, `!output`, etc.):

```csharp
namespace Assembler.Deserialisation.Dtos
{
    public sealed record <Name>RefDto : RefDto;
}
```

`RefDto` is the base; it gives you `Id`. The whole file is one line. Don't add more.

**Structured DTO** (multiple fields — for `!vec`, `!colour`, `!expr`):

```csharp
namespace Assembler.Deserialisation.Dtos
{
    public sealed record <Name>Dto
    {
        public object? X { get; init; }
        public object? Y { get; init; }
    }
}
```

Use `object?` for fields whose value could itself be another tagged node (e.g. `!expr` inside an
`X` field). Use a concrete type (`string?`, `int`, etc.) only when the field is guaranteed to be a
plain scalar in YAML.

Nullable reference types are on project-wide — annotate accordingly. Records are `sealed` by
default in this codebase.

---

## Step 3 — Create the type converter

Location: `Assets/Deserialisation/<Name>TypeConverter.cs`

Always `internal`, always implements `IYamlTypeConverter`, always `WriteYaml` throws — these
converters are read-only.

**Scalar converter** (copy from [VarTypeConverter](../../Assets/Deserialisation/VarTypeConverter.cs)):

```csharp
using System;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
    internal class <Name>TypeConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type) => type == typeof(<Name>RefDto);

        public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            var scalar = parser.Consume<Scalar>();
            return new <Name>RefDto { Id = scalar.Value };
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
            throw new NotSupportedException();
    }
}
```

**Mapping converter** — `parser.Consume<MappingStart>()`, then loop consuming key (`Scalar`) and
value (`rootDeserializer(typeof(object))`) until `MappingEnd`. Use `rootDeserializer` for values, not
`parser.Consume<Scalar>()`, so nested tagged nodes (e.g. `!expr`) inside fields parse correctly.
See [VecTypeConverter](../../Assets/Deserialisation/VecTypeConverter.cs).

**Polymorphic converter (scalar OR mapping)** — branch on `parser.Accept<Scalar>(out _)` vs
`parser.Accept<MappingStart>(out _)` *before* consuming. See
[ColourTypeConverter](../../Assets/Deserialisation/ColourTypeConverter.cs).

### Why `rootDeserializer` matters

When a field's value can itself be a tagged node (`X: !expr foo`), you must pass that value through
the full deserializer pipeline, not consume it as a raw scalar. `rootDeserializer(typeof(object))`
does this. Raw `Consume<Scalar>()` will silently drop the tag.

### Unknown keys

The existing mapping converters silently ignore keys they don't recognise (see VecTypeConverter's
switch — no `default`). Match that behaviour unless the user explicitly wants strict validation.
The transformation layer downstream will catch missing required fields.

---

## Step 4 — Register in GameFileParser

Edit [Assets/Deserialisation/GameFileParser.cs](../../Assets/Deserialisation/GameFileParser.cs).

Add one `.WithTagMapping(...)` line in the existing block (group with the other mappings), and one
`.WithTypeConverter(...)` line in the existing block (group with the other converters). Preserve
the existing ordering style — mappings first, converters second:

```csharp
.WithTagMapping("!<name>", typeof(<Name>Dto))
...
.WithTypeConverter(new <Name>TypeConverter())
```

That's the entire registration. Don't touch `ObjectNodeDeserializer` unless the tag needs special
handling that doesn't fit the converter model (rare — only `!vec` and `!colour` currently do, for
historical reasons around scalar/mapping coexistence inside generic `object?` fields).

---

## Step 5 — Verify

1. Open `GameFileParser.cs` and visually confirm both lines are present and ordered consistently.
   Then do a fast compile-only check that the converter builds (errors **and** warnings, no test run):
   `Tools/check-compile.sh` — it boots Unity in batch mode, parses the compiler output, prints a
   `Compile check` summary, and exits non-zero on any compiler error, so a typo surfaces in seconds
   before you write or run tests.
2. Add a parsing test if the user wants one. Pattern lives in
   `Assets/Tests/Parsing/TemplateTests.cs` — `new GameFileParser().Parse(yaml)` against an
   inline YAML string, then assert on the resulting DTO tree. Run it headlessly with
   `Tools/run-tests.sh Tests.Parsing` — this boots Unity in batch mode (via
   `Editor.TestBatch.RunEditModeTests`), prints a pass/fail summary, and exits non-zero on failure,
   so a parse regression or compile error in the converter surfaces without opening the editor.
3. Tell the user the tag is now parseable and ask how they want it consumed downstream
   (which Info record holds it? does it become a `ValueSource<T>`? etc.). That's a separate task.

---

## What this skill does NOT do

- Does **not** add the tag to the [Behaviours.md](../../Assets/docs/Behaviours.md) catalogue or any
  other docs — those describe behaviours, not tags.
- Does **not** thread the new DTO through `Transformer`, `*Info` records, `ValueResolver`, or
  `GameBehaviourFactory`. Those edits depend on what the tag *means* and require the user's design
  input.
- Does **not** edit example YAML files to demonstrate the new tag (unless the user asks).
- Does **not** touch the backend (`Assembler.Backend/`). The deserialisation layer lives entirely in
  the Unity project under `Assembler/Assets/Deserialisation/`.

If the user asks for any of the above as part of this task, do it as a follow-up after the
deserialisation layer is in place — they're separate concerns.
