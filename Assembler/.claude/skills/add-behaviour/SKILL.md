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
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Velocity")));

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
				ValueSourceFactory.CreateValueSource<AudioClip>(ctx, props.GetValueOrDefault("Clip")),
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("PlayOnStart")),
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("Loop")));

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
- Build each property with the **`ValueSourceFactory`** helpers (in `Assembler.Parsing`) — **not** `Transformer.CreateValueSource`, which no longer exists:
  - `ValueSourceFactory.CreateValueSource<T>(ctx, props.GetValueOrDefault("PropName"))` — the standard case; the `TransformContext` carries the values, parameters, expressions and type-registry the factory needs.
  - `ValueSourceFactory.CreateOptionalValueSource<T>(ctx, props.GetValueOrDefault("PropName"))` — for an optional property: a missing key resolves to `None<T>` (→ `NullValueProvider<T>` at runtime) instead of throwing, so the MonoBehaviour reads it with `.ValueOr(ctx, default)`.
  - `ValueSourceFactory.CreateEnumSource<TEnum>(ctx, props.GetValueOrDefault("PropName"), fallback)` / `CreateOptionalEnumSource<TEnum>(...)` — for enum-valued properties.
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

This is a `sealed class` that holds `IValueProvider<T>` properties for runtime (use `IWriteValueProvider<T>`
for any property the behaviour writes back to — see the write-back example below). **It no longer carries
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

### Write-back property — `IWriteValueProvider<T>`

A property the behaviour *writes to* (not just reads) is typed `IWriteValueProvider<T>`, which adds a
`Set(T value)` method. This is how the shared-velocity behaviours (`acceleration`, `drag`, `speed limit`)
mutate a `velocity` variable each frame, how `variable setter` writes its target, and how the AI behaviours
(`perceive`, `steering`, `navigate`) publish their outputs into variables.

```csharp
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class DragData : BehaviourData
	{
		public IWriteValueProvider<Vector3> Velocity { get; }   // written each frame via .Set(...)
		public IValueProvider<float> Coefficient { get; }       // read-only

		public DragData(string id,
			IWriteValueProvider<Vector3> velocity,
			IValueProvider<float> coefficient) :
			base(id) => (Velocity, Coefficient) = (velocity, coefficient);
	}
}
```

The builder resolves a write target with `.ResolveWritable(ctx.Resolution)` (Step 5). Only a `!var` reference
resolves to a writable provider; a constant/expression/clock value does not — `ResolveWritable` throws on
those, so a write-back property forces the YAML to point at a variable.

### Rules

- Regular behaviours extend `BehaviourData`, triggers extend `TriggerData`.
- Constructor signature is always: `(string id, ...IValueProvider<T> properties)`. **No `IReadOnlyList<Action> listeners` parameter** — that argument is now gone from data classes.
- Always call `base(id)`.
- **Properties MUST always be `IValueProvider<T>` (get-only) — or `IWriteValueProvider<T>` for write-back targets — never raw types like `float`, `bool`, `string`, etc.** This ensures values can be reactive at runtime (e.g. driven by variables, expressions, or references). The corresponding MonoBehaviour reads the value via `.Get(ctx)` — passing the `TriggerContext` that flowed into `Execute` so trigger outputs resolve (e.g. `Data.Delay.Get(ctx)`), and writes via `.Set(value)` on an `IWriteValueProvider<T>`. In a Unity callback with no upstream context, use the no-arg `.Get()` extension (which passes `TriggerContext.Empty`) or `.ValueOr(ctx, default)` for optional values. **There is no longer a `.Value` property.**
- Some data classes use **object-initialiser** properties (`{ get; init; }` set via `new TData(i.Id) { Prop = ... }`) rather than ctor parameters — see the collider/rigidbody entries. Either shape is fine; match what the behaviour needs.

---

## Step 3 — MonoBehaviour (with XML doc comments)

**Location:** `Assets/Behaviours/<Subcategory>/<Name>.cs`
**Namespace:** `Assembler.Behaviours.<Subcategory>`

Subcategories (the actual folders under `Assets/Behaviours/`): `Movement`, `Rotation`, `Animations`,
`Physics`, `Camera`, `Sprites`, `Audio`, `Spawners`, `Visual`, `Debug`, `Time`, `AI`, `ListOperations`,
`VariableUpdaters`, `UI`, `Triggers`, `Triggers.Conditionals`, `Triggers.Input`, `Triggers.Input.Touch`,
`Triggers.Physical`, `Triggers.Timing`, `Triggers.Variables`.

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
using Assembler.Time;

namespace Assembler.Behaviours.Movement
{
	/// <summary>Moves the entity each frame by <c>Velocity * deltaTime</c>.</summary>
	/// <remarks>
	/// Properties:
	///   Velocity: World-space velocity in units per second.
	/// </remarks>
	public class Velocity : GameBehaviour<VelocityData>, INeedsGameClock
	{
		// Auto-wired by the factory (like Spawner). Never read UnityEngine.Time directly —
		// Clock respects pause / slow-mo / deterministic replay.
		public IGameClock Clock { get; set; } = null!;

		private void Update()
		{
			Execute(TriggerContext.Empty);
		}

		public override void Execute(TriggerContext ctx)
		{
			transform.position += Data.Velocity.Get(ctx) * Clock.DeltaTime;
		}
	}
}
```

### Timing trigger

```csharp
using System.Collections;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;

namespace Assembler.Behaviours.Triggers.Timing
{
	/// <summary>Fires once after a delay (starts the countdown on entity start, or on Execute).</summary>
	/// <remarks>
	/// Properties:
	///   Delay: Seconds to wait before notifying listeners.
	/// </remarks>
	public class TimerTrigger : TimingTrigger<TimerTriggerData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private void Start()
		{
			Execute(TriggerContext.Empty);
		}

		public override void Execute(TriggerContext ctx)
		{
			var captured = ctx;
			StartCoroutine(InvokeTriggerAfter(Data.Delay.Get(ctx), captured));
		}

		// WaitForGameSeconds accumulates Clock.DeltaTime, so the wait freezes under pause / slow-mo.
		// Never use UnityEngine's WaitForSeconds for gameplay timers.
		private IEnumerator InvokeTriggerAfter(float seconds, TriggerContext captured)
		{
			yield return new WaitForGameSeconds(Clock, seconds);
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

Most input flows through the `Controls` section and the existing `input action` behaviour
(`InputActionTrigger`) — physical keys/mouse/gamepad are *bindings*, not bespoke triggers, so you
rarely need a new input trigger. When you do, inherit `InputTrigger<TData>`, read the new Input
System (`UnityEngine.InputSystem`), and notify from a Unity callback:

```csharp
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine.InputSystem;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Illustrative input trigger: fires every frame the bound device reports activity.</summary>
	public class ExampleInputTrigger : InputTrigger<ExampleInputTriggerData>
	{
		private void Update()
		{
			if (/* ...read UnityEngine.InputSystem, e.g. Gamepad.current?.aButton.isPressed... */ false)
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
- **Service dependencies are auto-wired via `INeeds*` marker interfaces.** Implement the matching interface and `GameBehaviourFactory.Create` injects the service after `AddComponent` and before `Initialise` runs — **do not assign these from the builder lambda.** The full set:
  - `INeedsSpawner { IEntitySpawner Spawner }` — entity spawning.
  - `INeedsGameClock { IGameClock Clock }` — game time (see the clock rule below).
  - `INeedsEntityQuery { EntityQueryService Query }` — querying entities by tag/proximity at runtime.
  - `INeedsLineOfSight { LineOfSightService Sight }` — line-of-sight raycasts (perception).
  - `INeedsNavigation { NavGridService Nav }` — grid navigation / pathfinding.

  (Note: `INeedsTriggerContext` no longer exists — `TriggerContext` is threaded as a method parameter, not injected.)
- **`Clock` is auto-wired — never read `UnityEngine.Time`.** Any behaviour that needs game time (per-frame motion, `Time.time` style timestamps, or coroutine waits) implements `INeedsGameClock { IGameClock Clock { get; set; } }` (in `Assembler.Time`) and reads `Clock.DeltaTime` / `Clock.Time` / `Clock.FrameCount`. The factory injects it exactly like `Spawner`, so **do not assign `Clock` from the builder lambda**. The clock respects pause, slow-mo (`set timescale`), and deterministic replay — reading `UnityEngine.Time` directly bypasses all of that. For coroutine delays use `new WaitForGameSeconds(Clock, seconds)`, not `WaitForSeconds`. Descriptor expressions get the same values via the `!clock <property>` value tag.

---

## Step 4 — Registry Entry

**Location:** `Assets/Parsing/BehaviourRegistry.cs`
**Add to:** The `All` dictionary inside `BehaviourRegistry`.

```csharp
["yaml key name"] = YourInfo.Create,
```

### Rules

- The dictionary key is the **YAML behaviour name** — lowercase, with spaces (e.g. `"timer trigger"`, `"input action"`).
- The value is the Info's static `Create` method reference. Property names and types come from reflecting the Info record at doc-gen time (see Step 1's `[YamlName]` rules).
- The `BehaviourFactory` delegate signature is `(string id, IReadOnlyList<ListenerInfo> listeners, IReadOnlyDictionary<string, AssemblerValue> props, TransformContext ctx) → BehaviourInfo`, which matches your Info's `Create` method exactly.

---

## Step 5 — Builder Entry (also handles doc-gen mapping)

**Location:** `Assets/Building/GameBehaviourFactory.cs`
**Add to:** The `map` dictionary inside `CreateBuilders()`.

The dictionary value is a `BuilderEntry` record that bundles **the MonoBehaviour type** with the **build
lambda**. The `MonoBehaviourByInfo` map used by doc generation is derived from this dictionary
automatically — **you no longer maintain a separate doc-gen mapping**.

**Prefer the typed `Entry<TInfo, TBehaviour, TData>(...)` helper for the common case.** It bundles the
cast → `AddComponent` → `Initialise(data, listeners)` boilerplate and ties the three types together so a
mismatched pairing fails to *compile* rather than at runtime. You only supply a `makeData` lambda
`(i, ctx) => new TData(...)`:

### Regular behaviour

```csharp
[typeof(VelocityInfo)] = Entry<VelocityInfo, Velocity, VelocityData>(
    (i, ctx) => new VelocityData(i.Id,
        i.Velocity.Resolve(ctx.Resolution))),
```

### Trigger

```csharp
[typeof(TimerTriggerInfo)] = Entry<TimerTriggerInfo, TimerTrigger, TimerTriggerData>(
    (i, ctx) => new TimerTriggerData(i.Id,
        i.Delay.Resolve(ctx.Resolution))),
```

### Physical trigger

The physical collision/trigger MonoBehaviours all derive from `GameBehaviour<PhysicalTriggerData>` (via
`PhysicalTrigger`), so the `TData` type parameter is the `PhysicalTriggerData` base — the concrete
`*TriggerData` the lambda builds upcasts to it:

```csharp
[typeof(CollisionEnterTriggerInfo)] = Entry<CollisionEnterTriggerInfo, CollisionEnter, PhysicalTriggerData>(
    (i, ctx) => new CollisionEnterTriggerData(i.Id,
        i.TagsToDetect)),
```

### Write-back property

Resolve a property the behaviour writes to with **`.ResolveWritable(ctx.Resolution)`** (→ `IWriteValueProvider<T>`),
the rest with `.Resolve(...)`:

```csharp
[typeof(DragInfo)] = Entry<DragInfo, DragBehaviour, DragData>(
    (i, ctx) => new DragData(i.Id,
        i.Velocity.ResolveWritable(ctx.Resolution),
        i.Coefficient.Resolve(ctx.Resolution))),
```

### Object-initialiser data

For data classes with `init` properties (colliders, rigidbody), build with an object initialiser inside the lambda:

```csharp
[typeof(BoxColliderInfo)] = Entry<BoxColliderInfo, AutoAddBoxColliderBehaviour, BoxColliderData>(
    (i, ctx) => new BoxColliderData(i.Id)
    {
        Size = i.Size.Resolve(ctx.Resolution),
        IsTrigger = i.IsTrigger.Resolve(ctx.Resolution),
    }),
```

### When `Entry<>` doesn't fit — the raw form

When the build needs more than "cast → add → initialise with resolved data" — a UI prefab lookup, a service
field that isn't covered by an `INeeds*` interface, declaring a variable up-front, custom validation — write
the entry directly. The lambda signature is
`(GameObject go, BehaviourInfo info, BehaviourBuildContext ctx) → (GameBehaviour, InitialiseBehaviourEvent)`:

```csharp
[typeof(ExclusiveTriggerInfo)] = new(typeof(ExclusiveTrigger), (go, info, ctx) =>
{
    var i = (ExclusiveTriggerInfo)info;
    var b = go.AddComponent<ExclusiveTrigger>();
    b.Registry = ctx.ExclusiveGroups;   // bespoke field not covered by an INeeds* interface
    return (b, lr => b.Initialise(new ExclusiveTriggerData(i.Id,
        i.Group.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
}),
```

Existing raw-form entries to model on: `TextLabel`/`UIButton`/`UISlider` (prefab via `RequireUiPrefab`),
`CameraFollow` (tag-target closure over the live registry), `StateMachine` (seeds its state variable),
`InputActionTrigger` (looks up the built `InputAction`). Assign bespoke fields directly on `b` before
returning. Only do this for things not already covered by an `INeeds*` injection — when in doubt, ask the
user rather than inventing a new field.

In both forms:

- Resolve property values via `i.Foo.Resolve(ctx.Resolution)` (where `ctx.Resolution` is a `ResolutionContext`), or `.ResolveWritable(...)` for write-back targets.
- Convert listeners via `i.Listeners.ToListeners(lr, ctx.Resolution)` — this returns `IReadOnlyList<Listener>`, which is what `Initialise` takes as its second argument. **Do not use the old `ToActions` extension.** (The `Entry<>` helper does this for you.)
- `Initialise` takes `(data, listeners)` as two separate arguments.

> Note: the `INeeds*` service fields (`Spawner`, `Clock`, `Query`, `Sight`, `Nav` — see Step 3) are
> auto-wired by `GameBehaviourFactory.Create` after the build lambda returns and before the
> `InitialiseBehaviourEvent` runs. The build lambda must **not** set them manually. `TriggerContext` is
> **not** injected — it is threaded through `Execute(ctx)` / `NotifyListeners(ctx)` at runtime, so there is
> nothing context-related to wire here.

### Rules

- Dictionary key is `typeof(YourInfo)`.
- **Default to `Entry<TInfo, TBehaviour, TData>((i, ctx) => new TData(...))`** — `TInfo`/`TBehaviour`/`TData` are constrained (`TInfo : BehaviourInfo`, `TBehaviour : GameBehaviour<TData>`, `TData : BehaviourData`), so a wrong pairing won't compile. It auto-handles the cast, `AddComponent`, listener conversion, and `Initialise`.
- The `TBehaviour` type argument (and the first arg of the raw `new(typeof(...), ...)` form) is what `MonoBehaviourByInfo` exposes for doc-gen — get it right or the generated docs will be wrong.
- For triggers whose MonoBehaviour derives from a shared data base (e.g. all physical triggers use `PhysicalTriggerData`), pass that base as `TData`.
- Resolve properties with `.Resolve(ctx.Resolution)`; write-back targets with `.ResolveWritable(ctx.Resolution)`.
- Use the **raw `new(typeof(YourBehaviour), (go, info, ctx) => { ... })` form** only when `Entry<>` doesn't fit (bespoke field, prefab lookup, validation, up-front variable). Then: cast `info` first, `go.AddComponent<YourBehaviour>()`, return `(behaviour, lr => b.Initialise(data, i.Listeners.ToListeners(lr, ctx.Resolution)))` where `lr` is `IReadOnlyBehaviourRegistry`. **Use `.ToListeners`, not the old `ToActions`.**
- Add using directives at the top of `GameBehaviourFactory.cs` for any new namespaces needed.

### Generic registrations

For generic Info types (e.g. `VariableSetterInfo<T>`, `ListAddInfo<T>`) the file uses helper methods that
add one entry per closed generic:

- `RegisterVariableSetter<T, TBehaviour>(map)` — variable setters.
- `RegisterVariableChangedTrigger<T, TBehaviour>(map)` — variable-changed triggers (validates the `!var` reference).
- `RegisterListOps<T, TAdd, TInsert, TRemoveAt, TRemove, TSetAt, TSet, TAddRange, TClear, TLoop>(map)` — the full set of list operations for one element type.

If you're adding a new type parameter for an existing generic behaviour, add a call to the existing helper
with your concrete `MonoBehaviour` subclass(es). If you're introducing a new generic behaviour, write a
similar helper that registers one `BuilderEntry` per closed generic instantiation, each pointing at its
non-generic MonoBehaviour subclass — the doc-walker climbs `BaseType` to find docs on the generic base.

---

## Checklist

When adding a new behaviour, create/modify these 5 things in order:

1. `Assets/Parsing/Info/Behaviours/<Name>Info.cs` — Info record with `Create(string id, listeners, props, TransformContext ctx)` and `SubstituteParameters(substitutedListeners, TransformContext ctx)`; add `[YamlName]` if YAML key ≠ property name.
2. `Assets/Resolving/Behaviours/<Name>Data.cs` — Data class with `(string id, IValueProvider<T> ...)` constructor and `base(id)` (use `IWriteValueProvider<T>` for write-back targets). **No listeners parameter.**
3. `Assets/Behaviours/<Subcategory>/<Name>.cs` — MonoBehaviour extending `GameBehaviour<TData>` (or a trigger base) with `Execute(TriggerContext ctx)` override and `<summary>` + `<remarks>` doc comments.
4. `Assets/Parsing/BehaviourRegistry.cs` — Add `["yaml key"] = YourInfo.Create` to `All`.
5. `Assets/Building/GameBehaviourFactory.cs` — Add `[typeof(YourInfo)] = Entry<YourInfo, YourBehaviour, YourData>((i, ctx) => new YourData(...))` to the `map` inside `CreateBuilders()` (drop to the raw `new(typeof(...), (go, info, ctx) => { ... })` form only when `Entry<>` doesn't fit). This single entry also drives doc-gen via `MonoBehaviourByInfo` — no separate step needed.

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

First do a fast compile-only check that your five files build (errors **and** warnings, no test run):

```bash
Tools/check-compile.sh   # surfaces compiler errors/warnings; non-zero on error
```

It boots Unity in batch mode, parses the compiler output, prints a `Compile check` summary, and exits
non-zero on any compiler error — the quickest way to catch a typo or a nullable warning in the
behaviour before running the (slower) tests. By default it reports diagnostics for the code that
recompiled (your new/changed files); add `--all` for a full-project sweep. Then run the tests
headlessly to confirm the behaviour works and nothing regressed:

```bash
Tools/run-tests.sh                 # all EditMode suites
Tools/run-tests.sh Tests.Behaviours  # or scope to one assembly to iterate faster
```

It boots Unity in batch mode and runs the same tests as Window > General > Test Runner (via
`Editor.TestBatch.RunEditModeTests`), prints a pass/fail summary, and exits non-zero on failure
(first run on a fresh worktree imports the project and is slow; later runs are fast). A compile error
in any of your five files surfaces here too. If you add a test for the behaviour, put it under
`Assets/Tests/Behaviours/` and re-run.
