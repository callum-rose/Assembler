---
name: add-behaviour
description: >
  Use this skill when the user asks to add a new behaviour, trigger, or component to the Assembler project.
  This includes creating new gameplay behaviours, input triggers, timing triggers, conditional triggers,
  physics triggers, or any new behaviour type that follows the 5-file pattern. Trigger this skill when the
  user says things like "add a behaviour", "create a new trigger", "add a rotation behaviour", etc.
---

# Adding a New Behaviour to Assembler

Every behaviour requires **6 coordinated changes** across 5 locations. All 6 must be created together
or the pipeline (and/or doc generation) will fail. Follow each step exactly, matching the code style shown.

> **Critical**: Use tabs for indentation in all files. Match the existing code style precisely.

---

## Step 1 — Info Record

**Location:** `Assets/Parsing/Info/Behaviours/<Name>Info.cs`
**Namespace:** `Assembler.Parsing.Info.Behaviours`

This is a C# `record` that extends `BehaviourInfo`. Properties are `ValueSource<T>`.

### Single-property example (Velocity)

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record VelocityInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<Vector3> Velocity)
		: BehaviourInfo(Id, Listeners)
	{
		public static VelocityInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Velocity")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new VelocityInfo(Id,
				substitutedListeners,
				Velocity.SubstituteParameters(ctx));
	}
}
```

### Multi-property example (AudioSource)

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record AudioSourceInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<AudioClip> Clip,
		ValueSource<bool> PlayOnStart,
		ValueSource<bool> Loop)
		: BehaviourInfo(Id, Listeners)
	{
		public static AudioSourceInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<AudioClip>(ctx, props.GetValueOrDefault("Clip")),
				Transformer.CreateValueSource<bool>(ctx, props.GetValueOrDefault("PlayOnStart")),
				Transformer.CreateValueSource<bool>(ctx, props.GetValueOrDefault("Loop")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new AudioSourceInfo(Id,
				substitutedListeners,
				Clip.SubstituteParameters(ctx),
				PlayOnStart.SubstituteParameters(ctx),
				Loop.SubstituteParameters(ctx));
	}
}
```

### Rules

- Record extends `BehaviourInfo(Id, Listeners)` — always pass both through.
- Each property is `ValueSource<T>`.
- `Create` signature is always: `(string id, IReadOnlyList<ListenerInfo> listeners, IReadOnlyDictionary<string, AssemblerValue> props, TransformContext ctx)`.
- Use `Transformer.CreateValueSource<T>(ctx, props.GetValueOrDefault("PropName"))` for each property — the `TransformContext` carries the values, parameters, expressions and type-registry the transformer needs.
- **Default convention:** the record property name *is* the YAML key. Doc generation reflects the record's primary-ctor params to build the property list — so prefer matching the YAML key to the property name (e.g. record param `Velocity` ↔ YAML key `Velocity`).
- **`[YamlName]` override:** if the YAML key cannot match the record property name (e.g. `VariableSetterInfo`'s `ValueToSet` is exposed in YAML as `VariableId`), annotate the param with `[property: YamlName("YamlKey")]`:

  ```csharp
  public record VariableSetterInfo<T>(
      string Id,
      IReadOnlyList<ListenerInfo> Listeners,
      [property: YamlName("VariableId")] ValueSource<T> ValueToSet,
      [property: YamlName("Value")] ValueSource<T> ValueToGet) : BehaviourInfo(Id, Listeners) { ... }
  ```

  Doc-gen reads the attribute via reflection; the YAML key in `props.GetValueOrDefault("...")` must still match what `[YamlName]` declares. `YamlNameAttribute` lives in `Assembler.Parsing.Info`.
- `SubstituteParameters` calls `.SubstituteParameters(ctx)` on every `ValueSource<T>` property — the `TransformContext` carries the substitution scope.
- For triggers, the Info record is identical in structure — it still extends `BehaviourInfo`, not a trigger-specific base.

---

## Step 2 — Data Class

**Location:** `Assets/Resolving/Behaviours/<Name>Data.cs`
**Namespace:** `Assembler.Resolving.Behaviours`

This is a `sealed class` that holds `IValueProvider<T>` properties for runtime. **It no longer carries
listeners** — listeners are passed separately into `Initialise` (see Step 3 / Step 5).

### Regular behaviour — extends `BehaviourData`

```csharp
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class VelocityData : BehaviourData
	{
		public IValueProvider<Vector3> Velocity { get; }

		public VelocityData(string id, IValueProvider<Vector3> velocity) :
			base(id) => Velocity = velocity;
	}
}
```

### Trigger — extends `TriggerData`

```csharp
namespace Assembler.Resolving.Behaviours
{
	public sealed class TimerTriggerData : TriggerData
	{
		public IValueProvider<float> Delay { get; }

		public TimerTriggerData(string id, IValueProvider<float> delay) :
			base(id) => Delay = delay;
	}
}
```

### Rules

- Regular behaviours extend `BehaviourData`, triggers extend `TriggerData`.
- Constructor signature is always: `(string id, ...IValueProvider<T> properties)`. **No `IReadOnlyList<Action> listeners` parameter** — that argument is now gone from data classes.
- Always call `base(id)`.
- **Properties MUST always be `IValueProvider<T>` (get-only), never raw types like `float`, `bool`, `string`, etc.** This ensures values can be reactive at runtime (e.g. driven by variables, expressions, or references). The corresponding MonoBehaviour reads the value via `.Get(ctx)` — passing the `TriggerContext` that flowed into `Execute` so trigger outputs resolve (e.g. `Data.Delay.Get(ctx)`). In a Unity callback with no upstream context, use the no-arg `.Get()` extension (which passes `TriggerContext.Empty`) or `.ValueOr(ctx, default)` for optional values. **There is no longer a `.Value` property.**

---

## Step 3 — MonoBehaviour (with XML doc comments)

**Location:** `Assets/Behaviours/<Subcategory>/<Name>.cs`
**Namespace:** `Assembler.Behaviours.<Subcategory>`

Subcategories: `Movement`, `Rotation`, `Animations`, `Physics`, `Camera`, `Sprites`, `Audio`, `Spawners`,
`Visual`, `Debug`, `Debug.UI`, `VariableUpdaters`, `ListOperations`, `Triggers.Input`, `Triggers.Timing`,
`Triggers.Conditionals`, `Triggers.Physical`.

> **The MonoBehaviour is the documentation home.** The doc generator (`Tools/generate-docs.sh`, or the
> `Assembler > Generate Behaviour Docs` menu item) reads the `<summary>` and `<remarks>` XML doc
> comments above the class declaration to build the AI-facing `Assets/docs/Behaviours.md`.
> **Author docs here, not on the Info record.**
> Doc-gen validates the property set: any Info property missing from your `Properties:` block (or any
> extra `Properties:` entry not on Info) emits a warning in the Editor console and in the markdown.

### Doc-comment contract

Every MonoBehaviour declares (above `public class ...`):

```csharp
/// <summary>One-sentence behaviour description.</summary>
/// <remarks>
/// Properties:
///   PropName: human description.
///   AnotherProp [TypeOverride]: description; bracketed type overrides the rendered .NET type.
/// Outputs:
///   output_name [Type]: description.
/// </remarks>
```

- **`<summary>`** — required. One sentence on what the behaviour does.
- **`Properties:` block** — one line per Info property. Names must match the YAML keys (i.e. record
  property names, or the `[YamlName]` override if present). The block must be present even if empty
  (`Properties:` on its own line is fine for behaviours with no properties — emit it so the parser knows).
- **`Outputs:` block** — only required when the MonoBehaviour publishes outputs by adding keys to the
  `TriggerContext` it passes to `NotifyListeners` (e.g. physical triggers, UI controls). Names must
  match the literal string keys written into the context (via `ctx.With("name", value)` or the
  builder-form `ctx.With(b => b["name"] = value)`). The bracketed type is part of the doc — output
  types are not otherwise discoverable from the Info record.
- **Generic bases:** author the doc once on the generic base (e.g. `VariableSetterBehaviour<T>`,
  `ListAddBehaviour<T>`) — closed subclasses (`Vector3Setter`, `IntListAdd`, …) inherit it via the
  doc-walker climbing `BaseType`. Don't repeat the doc on each closed type.

### Regular behaviour

```csharp
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Movement
{
	/// <summary>Moves the entity each frame by <c>Velocity * deltaTime</c>.</summary>
	/// <remarks>
	/// Properties:
	///   Velocity: World-space velocity in units per second.
	/// </remarks>
	public class Velocity : GameBehaviour<VelocityData>
	{
		private void Update()
		{
			Execute(TriggerContext.Empty);
		}

		public override void Execute(TriggerContext ctx)
		{
			transform.position += Data.Velocity.Get(ctx) * Time.deltaTime;
		}
	}
}
```

### Timing trigger

```csharp
using System.Collections;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Timing
{
	/// <summary>Fires once after a delay (starts the countdown on entity start, or on Execute).</summary>
	/// <remarks>
	/// Properties:
	///   Delay: Seconds to wait before notifying listeners.
	/// </remarks>
	public class TimerTrigger : TimingTrigger<TimerTriggerData>
	{
		private void Start()
		{
			Execute(TriggerContext.Empty);
		}

		public override void Execute(TriggerContext ctx)
		{
			var captured = ctx;
			StartCoroutine(InvokeTriggerAfter(Data.Delay.Get(ctx), captured));
		}

		private IEnumerator InvokeTriggerAfter(float seconds, TriggerContext captured)
		{
			yield return new WaitForSeconds(seconds);
			NotifyListeners(captured);
		}
	}
}
```

### Physical trigger that publishes outputs

```csharp
using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	/// <summary>Fires when a non-trigger collision begins with another entity matching TagsToDetect.</summary>
	/// <remarks>
	/// Properties:
	///   TagsToDetect: Only fire when the other entity has at least one of these tags.
	/// Outputs:
	///   contact_point [Vector3]: World-space point of first contact.
	///   other_position [Vector3]: Other entity's world position at the moment of collision.
	/// </remarks>
	public class CollisionEnter : PhysicalTrigger
	{
		private void OnCollisionEnter(Collision other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				NotifyListeners(TriggerContext.Empty.With(b =>
				{
					b["contact_point"] = other.contacts[0].point;
					b["other_position"] = other.transform.position;
				}));
			}
		}
	}
}
```

> Outputs are attached by deriving a new immutable `TriggerContext` and passing it to
> `NotifyListeners`. Use the builder-form `TriggerContext.Empty.With(b => { b["key"] = value; })` when
> setting several keys at once (one allocation), or chain `ctx.With("key", value)` for a single output.
> A trigger that wants to *add* outputs while preserving upstream ones starts from the `ctx` it received
> rather than `TriggerContext.Empty`.

### Input trigger

```csharp
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Fires every frame while the named key is held down.</summary>
	/// <remarks>
	/// Properties:
	///   Key: One of "w", "a", "s", "d", "up", "down", "left", "right".
	/// </remarks>
	public class KeyHoldTrigger : InputTrigger<KeyHoldTriggerData>
	{
		private void Update()
		{
			if (UnityEngine.Input.GetKey(/* ...derived from Data.Key.Get()... */))
			{
				NotifyListeners(TriggerContext.Empty);
			}
		}
	}
}
```

### Inheritance hierarchy

| Base class | Use for | `Execute(TriggerContext ctx)` |
|---|---|---|
| `GameBehaviour<TData>` | Regular behaviours | Must override — contains the main logic |
| `Trigger<TData>` | Generic triggers (extends `GameBehaviour<TData>`) | Must override |
| `TimingTrigger<TData>` | Time-based triggers (extends `Trigger<TData>`) | Must override |
| `InputTrigger<TData>` | User input triggers (extends `Trigger<TData>`) | Throws — input triggers are driven by Unity input callbacks, not `Execute(ctx)` |
| `PhysicalTrigger` | Collision/physics triggers (uses `PhysicalTriggerData`) | Throws — driven by Unity collision callbacks |

### Rules

- Access resolved data via the `Data` property, reading each provider with `.Get(ctx)` — thread through the `ctx` passed into `Execute` (e.g. `Data.Velocity.Get(ctx)`). There is **no `.Value` property**. In a Unity callback that has no upstream context, call the no-arg `.Get()` extension (it passes `TriggerContext.Empty`) or `.ValueOr(ctx, default)` for optional/nullable values.
- `Execute` is now `public override void Execute(TriggerContext ctx)`. It is abstract and must be overridden (except `InputTrigger` / `PhysicalTrigger`, which provide a throwing default). Unity-callback entry points (`Update`, `Start`, `OnGUI`, collision callbacks, coroutines) start a fresh chain by calling `Execute(TriggerContext.Empty)` or `NotifyListeners(TriggerContext.Empty)`.
- Call `NotifyListeners(ctx)` to fire downstream listeners — it now **takes the `TriggerContext`** and threads it to each listener's `Execute`. The base `GameBehaviour` owns the listeners list — you do not manage it on the MonoBehaviour yourself.
- **Publishing outputs:** there is no ambient `TriggerContext.Set(...)` any more. Derive a new immutable context and pass it to `NotifyListeners`: `NotifyListeners(ctx.With("key", value))`, or the batch builder form `NotifyListeners(ctx.With(b => { b["a"] = x; b["b"] = y; }))`. Start from the received `ctx` to cascade upstream outputs downstream, or from `TriggerContext.Empty` at a Unity-callback entry point. The key strings must match the `Outputs:` doc block.
- `Initialise(data, listeners)` is called by the builder (see Step 5). The base `GameBehaviour<TData>` stores the data and wires up the listeners automatically. Override `OnInitialise(TData data)` if you need extra one-time setup.
- **`Spawner` is auto-wired.** Behaviours that need `IEntitySpawner` implement `INeedsSpawner`; the factory injects it after `AddComponent` and before `Initialise` runs. **Do not assign `Spawner` from the builder lambda.** (Note: `INeedsTriggerContext` no longer exists — `TriggerContext` is threaded as a method parameter, not injected.)

---

## Step 4 — Registry Entry

**Location:** `Assets/Parsing/BehaviourRegistry.cs`
**Add to:** The `All` dictionary inside `BehaviourRegistry`.

```csharp
["yaml key name"] = YourInfo.Create,
```

### Rules

- The dictionary key is the **YAML behaviour name** — lowercase, with spaces (e.g. `"timer trigger"`, `"key hold trigger"`).
- The value is the Info's static `Create` method reference. Property names and types come from reflecting the Info record at doc-gen time (see Step 1's `[YamlName]` rules).
- The `BehaviourFactory` delegate signature is `(string id, IReadOnlyList<ListenerInfo> listeners, IReadOnlyDictionary<string, AssemblerValue> props, TransformContext ctx) → BehaviourInfo`, which matches your Info's `Create` method exactly.

---

## Step 5 — Builder Entry (also handles doc-gen mapping)

**Location:** `Assets/Building/GameBehaviourFactory.cs`
**Add to:** The `map` dictionary inside `CreateBuilders()`.

Each entry is a `BuilderEntry` record that bundles **the MonoBehaviour type** with the **build lambda**.
The `MonoBehaviourByInfo` map used by doc generation is derived from this dictionary automatically —
**you no longer maintain a separate doc-gen mapping**.

The lambda signature is always: `(GameObject go, BehaviourInfo info, BehaviourBuildContext ctx) → (GameBehaviour, InitialiseBehaviourEvent)`.

- Resolve property values via `i.Foo.Resolve(ctx.Resolution)` (where `ctx.Resolution` is a `ResolutionContext`).
- Convert listeners via `i.Listeners.ToListeners(lr, ctx.Resolution)` — this returns `IReadOnlyList<Listener>`, which is what `Initialise` takes as its second argument. **Do not use the old `ToActions` extension.**
- `Initialise` takes `(data, listeners)` as two separate arguments.

### Regular behaviour

```csharp
[typeof(VelocityInfo)] = new(typeof(Velocity), (go, info, ctx) =>
{
    var i = (VelocityInfo)info;
    var b = go.AddComponent<Velocity>();
    return (b, lr => b.Initialise(new VelocityData(i.Id,
        i.Velocity.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
}),
```

### Trigger

```csharp
[typeof(TimerTriggerInfo)] = new(typeof(TimerTrigger), (go, info, ctx) =>
{
    var i = (TimerTriggerInfo)info;
    var b = go.AddComponent<TimerTrigger>();
    return (b, lr => b.Initialise(new TimerTriggerData(i.Id,
        i.Delay.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
}),
```

### Physical trigger

```csharp
[typeof(CollisionEnterTriggerInfo)] = new(typeof(CollisionEnter), (go, info, ctx) =>
{
    var i = (CollisionEnterTriggerInfo)info;
    var b = go.AddComponent<CollisionEnter>();
    return (b, lr => b.Initialise(new CollisionEnterTriggerData(i.Id,
        i.TagsToDetect), i.Listeners.ToListeners(lr, ctx.Resolution)));
}),
```

> Note: `Spawner` is auto-wired by `GameBehaviourFactory.Create` via the `INeedsSpawner` interface,
> immediately after the build lambda returns and before the `InitialiseBehaviourEvent` runs. The build
> lambda must **not** set it manually. `TriggerContext` is **not** injected — it is threaded through
> `Execute(ctx)` / `NotifyListeners(ctx)` at runtime, so there is nothing context-related to wire here.

### Special context fields (rare)

Some behaviours still need bespoke fields from `BehaviourBuildContext` that aren't covered by the
interfaces above — for example, `ExclusiveTrigger` needs the `ExclusiveGroups` registry:

```csharp
[typeof(ExclusiveTriggerInfo)] = new(typeof(ExclusiveTrigger), (go, info, ctx) =>
{
    var i = (ExclusiveTriggerInfo)info;
    var b = go.AddComponent<ExclusiveTrigger>();
    b.Registry = ctx.ExclusiveGroups;
    return (b, lr => b.Initialise(new ExclusiveTriggerData(i.Id,
        i.Group.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
}),
```

Assign these directly on `b` before returning. Only do this for things not already covered by an
interface-based injection — when in doubt, ask the user rather than inventing a new field.

### Rules

- Dictionary key is `typeof(YourInfo)`.
- Entry is `new BuilderEntry(typeof(YourBehaviour), lambda)` — written as `new(typeof(YourBehaviour), (go, info, ctx) => { ... })` because `BuilderEntry` is the dictionary's value type and target-typed `new` is used.
- The first argument (`typeof(YourBehaviour)`) is what `MonoBehaviourByInfo` will expose for doc-gen — get it right or the generated docs will be wrong.
- Lambda signature is `(go, info, ctx)`.
- Cast `info` to the specific Info type as the first line of the lambda.
- `go.AddComponent<YourBehaviour>()` adds the MonoBehaviour to the GameObject.
- Return a tuple: `(GameBehaviour, InitialiseBehaviourEvent)`.
- The `InitialiseBehaviourEvent` is a lambda `lr => b.Initialise(data, listeners)` where `lr` is `IReadOnlyBehaviourRegistry`.
- Resolve properties with `.Resolve(ctx.Resolution)`.
- Convert listeners with `.ToListeners(lr, ctx.Resolution)` — `IReadOnlyList<Listener>`, not `IReadOnlyList<Action>`.
- Add using directives at the top of `GameBehaviourFactory.cs` for any new namespaces needed.

### Generic registrations

For generic Info types (e.g. `VariableSetterInfo<T>`, `ListAddInfo<T>`) the file uses helper methods —
`RegisterVariableSetter<T, TBehaviour>(map)` and `RegisterListOps<T, TAdd, TRemoveAt, TSetAt, TClear>(map)`
— that add one entry per closed generic. If you're adding a new type parameter for an existing generic
behaviour, add a call to the existing helper with your concrete `MonoBehaviour` subclass. If you're
introducing a new generic behaviour, write a similar helper that registers one `BuilderEntry` per closed
generic instantiation, each pointing at its non-generic MonoBehaviour subclass — the doc-walker climbs
`BaseType` to find docs on the generic base.

---

## Checklist

When adding a new behaviour, create/modify these 5 things in order:

1. `Assets/Parsing/Info/Behaviours/<Name>Info.cs` — Info record with `Create(string id, listeners, props, TransformContext ctx)` and `SubstituteParameters(substitutedListeners, TransformContext ctx)`; add `[YamlName]` if YAML key ≠ property name.
2. `Assets/Resolving/Behaviours/<Name>Data.cs` — Data class with `(string id, IValueProvider<T> ...)` constructor and `base(id)`. **No listeners parameter.**
3. `Assets/Behaviours/<Subcategory>/<Name>.cs` — MonoBehaviour extending `GameBehaviour<TData>` (or a trigger base) with `Execute(TriggerContext ctx)` override and `<summary>` + `<remarks>` doc comments.
4. `Assets/Parsing/BehaviourRegistry.cs` — Add `["yaml key"] = YourInfo.Create` to `All`.
5. `Assets/Building/GameBehaviourFactory.cs` — Add `[typeof(YourInfo)] = new(typeof(YourBehaviour), (go, info, ctx) => { ... })` to the `map` inside `CreateBuilders()`. (This single entry also drives doc-gen via `MonoBehaviourByInfo` — no separate step needed.)

**Do not forget any step.** A missing registry or builder entry will cause a runtime error when the
YAML references the behaviour; a wrong MonoBehaviour type in the `BuilderEntry` will produce warnings
or wrong docs when the docs are regenerated.

After authoring the files, regenerate and check the docs yourself by running the headless script:

```bash
Tools/generate-docs.sh
```

It boots Unity in batch mode and runs the same generator as the `Assembler > Generate Behaviour Docs`
menu item (first run on a fresh worktree imports the project and is slow; later runs are fast). Then
read your new behaviour's section in `Assets/docs/Behaviours.md` and the `## Doc-gen warnings` section
at the bottom — fix any warning that names your behaviour (a missing/extra `Properties:` entry, or a
wrong MonoBehaviour mapping). The in-editor menu item still works if you'd rather run it there.
