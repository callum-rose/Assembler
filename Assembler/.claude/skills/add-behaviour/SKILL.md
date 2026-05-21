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
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(v, props.GetValueOrDefault("Velocity"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new VelocityInfo(Id,
				substitutedListeners,
				Velocity.SubstituteParameters(parameters, allValues));
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
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<AudioClip>(v, props.GetValueOrDefault("Clip"), parameters: p),
				Transformer.CreateValueSource<bool>(v, props.GetValueOrDefault("PlayOnStart"), parameters: p),
				Transformer.CreateValueSource<bool>(v, props.GetValueOrDefault("Loop"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new AudioSourceInfo(Id,
				substitutedListeners,
				Clip.SubstituteParameters(parameters, allValues),
				PlayOnStart.SubstituteParameters(parameters, allValues),
				Loop.SubstituteParameters(parameters, allValues));
	}
}
```

### Rules

- Record extends `BehaviourInfo(Id, Listeners)` — always pass both through.
- Each property is `ValueSource<T>`.
- `Create` signature is always: `(string id, IReadOnlyList<ListenerInfo> listeners, IReadOnlyDictionary<string, AssemblerValue> props, IReadOnlyList<ValueInfo> v, IReadOnlyDictionary<string, AssemblerValue> p)`.
- Use `Transformer.CreateValueSource<T>(v, props.GetValueOrDefault("PropName"), parameters: p)` for each property.
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
- `SubstituteParameters` calls `.SubstituteParameters(parameters, allValues)` on every `ValueSource<T>` property.
- For triggers, the Info record is identical in structure — it still extends `BehaviourInfo`, not a trigger-specific base.

---

## Step 2 — Data Class

**Location:** `Assets/Resolving/Behaviours/<Name>Data.cs`
**Namespace:** `Assembler.Resolving.Behaviours`

This is a `sealed class` that holds `IValueProvider<T>` properties for runtime.

### Regular behaviour — extends `BehaviourData`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class VelocityData : BehaviourData
	{
		public IValueProvider<Vector3> Velocity { get; }

		public VelocityData(string id, IReadOnlyList<Action> listeners, IValueProvider<Vector3> velocity) :
			base(id, listeners) => Velocity = velocity;
	}
}
```

### Trigger — extends `TriggerData`

```csharp
using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class TimerTriggerData : TriggerData
	{
		public IValueProvider<float> Delay { get; }

		public TimerTriggerData(string id, IValueProvider<float> delay, IReadOnlyList<Action> listeners) :
			base(id, listeners) => Delay = delay;
	}
}
```

### Rules

- Regular behaviours extend `BehaviourData`, triggers extend `TriggerData`.
- Constructor signature: `(string id, IReadOnlyList<Action> listeners, ...IValueProvider<T> properties)` for regular behaviours.
- For triggers, the parameter order may place properties before listeners — match existing trigger patterns: `(string id, IValueProvider<T> prop, IReadOnlyList<Action> listeners)`.
- Always call `base(id, listeners)`.
- **Properties MUST always be `IValueProvider<T>` (get-only), never raw types like `float`, `bool`, `string`, etc.** This ensures values can be reactive at runtime (e.g. driven by variables, expressions, or references). The corresponding MonoBehaviour accesses the value via `.Value` (e.g. `Data.Delay.Value`).

---

## Step 3 — MonoBehaviour (with XML doc comments)

**Location:** `Assets/Behaviours/<Subcategory>/<Name>.cs`
**Namespace:** `Assembler.Behaviours.<Subcategory>`

Subcategories: `Movement`, `Physics`, `Camera`, `Sprites`, `Audio`, `Spawners`, `Debug`, `Debug.UI`,
`VariableUpdaters`, `ListOperations`, `Triggers.Input`, `Triggers.Timing`, `Triggers.Conditionals`,
`Triggers.Physical`.

> **The MonoBehaviour is the documentation home.** `Assembler > Generate Behaviour Docs` reads the
> `<summary>` and `<remarks>` XML doc comments above the class declaration to build the AI-facing
> [`Assets/docs/Behaviours.md`](../../docs/Behaviours.md). **Author docs here, not on the Info record.**
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
- **`Outputs:` block** — only required when the MonoBehaviour publishes outputs via
  `TriggerContext.Set("name", value)` (e.g. physical triggers, UI controls). Names must match the
  literal strings passed to `TriggerContext.Set`. The bracketed type is part of the doc — output
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
			Execute();
		}

		public override void Execute()
		{
			transform.position += Data.Velocity.Value * Time.deltaTime;
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
			Execute();
		}

		public override void Execute()
		{
			StartCoroutine(InvokeTriggerAfter(Data.Delay.Value));
		}

		private IEnumerator InvokeTriggerAfter(float seconds)
		{
			yield return new WaitForSeconds(seconds);
			NotifyListeners();
		}
	}
}
```

### Physical trigger that publishes outputs

```csharp
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
				TriggerContext.Push();
				try
				{
					TriggerContext.Set("contact_point", other.contacts[0].point);
					TriggerContext.Set("other_position", other.transform.position);
					NotifyListeners();
				}
				finally
				{
					TriggerContext.Pop();
				}
			}
		}
	}
}
```

### Input trigger

```csharp
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
			if (UnityEngine.Input.GetKey(/* ... */))
			{
				NotifyListeners();
			}
		}
	}
}
```

### Inheritance hierarchy

| Base class | Use for | `Execute()` |
|---|---|---|
| `GameBehaviour<TData>` | Regular behaviours | Must override — contains the main logic |
| `Trigger<TData>` | Generic triggers (extends `GameBehaviour<TData>`) | Must override — has `TriggerContext` property |
| `TimingTrigger<TData>` | Time-based triggers (extends `Trigger<TData>`) | Must override |
| `InputTrigger<TData>` | User input triggers (extends `Trigger<TData>`) | Throws — input triggers are driven by Unity input callbacks, not `Execute()` |

### Rules

- Access resolved data via the `Data` property (e.g. `Data.Velocity.Value`).
- Call `NotifyListeners()` to fire downstream listeners.
- `Execute()` is abstract and must be overridden (except `InputTrigger` which provides a throwing default).
- For triggers that need `TriggerContext` set before initialisation, see the builder step — it's set on the component before the init lambda runs.

---

## Step 4 — Registry Entry

**Location:** `Assets/Parsing/BehaviourRegistry.cs`
**Add to:** The `All` dictionary inside `BehaviourRegistry`.

```csharp
["yaml key name"] = YourInfo.Create,
```

### Rules

- The dictionary key is the **YAML behaviour name** — lowercase, with spaces (e.g. `"timer trigger"`, `"key hold trigger"`).
- The value is just the Info's static `Create` method reference. **No more `PropDescriptor[]`** — property names and types come from reflecting the Info record at doc-gen time (see Step 1's `[YamlName]` rules).

---

## Step 5 — Builder Entry

**Location:** `Assets/Building/GameBehaviourFactory.cs`
**Add to:** The `Builders` dictionary inside `GameBehaviourFactory`.

### Regular behaviour

```csharp
[typeof(VelocityInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
{
	var i = (VelocityInfo)info;
	var b = go.AddComponent<Velocity>();

	return (b, lr => b.Initialise(new VelocityData(i.Id,
		i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
		i.Velocity.Resolve(vr, cr, ar, tc, scope))));
},
```

### Trigger

```csharp
[typeof(TimerTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
{
	var i = (TimerTriggerInfo)info;
	var b = go.AddComponent<TimerTrigger>();

	return (b, lr => b.Initialise(new TimerTriggerData(i.Id,
		i.Delay.Resolve(vr, cr, ar, tc, scope),
		i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
},
```

### Trigger needing TriggerContext (collision/physics triggers, UI triggers)

```csharp
[typeof(CollisionEnterTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
{
	var i = (CollisionEnterTriggerInfo)info;
	var b = go.AddComponent<CollisionEnter>();
	b.TriggerContext = tc;

	return (b, lr => b.Initialise(new CollisionEnterTriggerData(i.Id,
		i.TagsToDetect,
		i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
},
```

### Rules

- Dictionary key is `typeof(YourInfo)`.
- Lambda signature is always: `(go, info, vr, cr, es, ar, tc, scope)`.
- Cast `info` to the specific Info type.
- `go.AddComponent<YourBehaviour>()` adds the MonoBehaviour to the GameObject.
- Return a tuple: `(GameBehaviour, InitialiseBehaviourEvent)`.
- The `InitialiseBehaviourEvent` is a lambda `lr => ...` where `lr` is `IReadOnlyBehaviourRegistry`.
- Resolve properties with `.Resolve(vr, cr, ar, tc, scope)`.
- Convert listeners with `.ToActions(lr, vr, cr, ar, tc, scope)`.
- For triggers that need `TriggerContext`, set `b.TriggerContext = tc` **before** the return.
- For behaviours that need `IEntitySpawner`, set `b.Spawner = es` **before** the return.
- Add using directives at the top of `GameBehaviourFactory.cs` for any new namespaces needed.

---

## Step 6 — Doc-gen Mapping (`MonoBehaviourByInfo`)

**Location:** `Assets/Building/GameBehaviourFactory.cs`
**Add to:** The `MonoBehaviourByInfo` dictionary (immediately after `Builders`).

```csharp
[typeof(YourInfo)] = typeof(YourBehaviour),
```

For generic Info records (like `VariableSetterInfo<T>` and `ListAddInfo<T>`) — one entry per closed
generic instantiation that the registry exposes, each pointing at its non-generic MonoBehaviour
subclass:

```csharp
[typeof(VariableSetterInfo<Vector3>)] = typeof(Vector3Setter),
[typeof(VariableSetterInfo<int>)] = typeof(IntSetter),
```

### Rules

- This map is what `Assets/Editor/BehaviourDocs.cs` uses to look up the MonoBehaviour for each Info
  type. Missing entries surface as `no MonoBehaviour mapping` warnings in the generated doc.
- For generic MonoBehaviours: still map to the **closed non-generic subclass** (e.g. `Vector3Setter`),
  not the open generic base — the doc-walker climbs `BaseType` automatically to find the docs on the
  generic base.
- This is doc-gen wiring only; runtime construction goes through `Builders` (Step 5).

---

## Checklist

When adding a new behaviour, create/modify these 6 things in order:

1. `Assets/Parsing/Info/Behaviours/<Name>Info.cs` — Info record with `Create` and `SubstituteParameters` (with `[YamlName]` if YAML key ≠ property name)
2. `Assets/Resolving/Behaviours/<Name>Data.cs` — Data class with `IValueProvider<T>` properties
3. `Assets/Behaviours/<Subcategory>/<Name>.cs` — MonoBehaviour with `Execute()` override **and `<summary>` + `<remarks>` doc comments**
4. `Assets/Parsing/BehaviourRegistry.cs` — Add `["yaml key"] = YourInfo.Create` to `All`
5. `Assets/Building/GameBehaviourFactory.cs` — Add entry to `Builders` dictionary
6. `Assets/Building/GameBehaviourFactory.cs` — Add entry to `MonoBehaviourByInfo` for doc generation

**Do not forget any step.** A missing registry or builder entry will cause a runtime error when the
YAML references the behaviour; a missing `MonoBehaviourByInfo` entry or missing/mismatched doc comments
will produce warnings when running `Assembler > Generate Behaviour Docs` but won't break runtime.

After adding the behaviour, run `Assembler > Generate Behaviour Docs` from the Unity Editor menu and
confirm there are no warnings about your new behaviour in the console or the `## Doc-gen warnings`
section of [`Assets/docs/Behaviours.md`](../../docs/Behaviours.md).
