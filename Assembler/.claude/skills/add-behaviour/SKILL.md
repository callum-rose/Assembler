---
name: add-behaviour
description: >
  Use this skill when the user asks to add a new behaviour, trigger, or component to the Assembler project.
  This includes creating new gameplay behaviours, input triggers, timing triggers, conditional triggers,
  physics triggers, or any new behaviour type that follows the 5-file pattern. Trigger this skill when the
  user says things like "add a behaviour", "create a new trigger", "add a rotation behaviour", etc.
---

# Adding a New Behaviour to Assembler

Every behaviour requires **5 coordinated changes** across 4 locations. All 5 must be created together
or the pipeline will fail. Follow each step exactly, matching the code style shown.

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
- The string passed to `GetValueOrDefault` must **exactly match** the `PropDescriptor` name in the registry (Step 4).
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

## Step 3 — MonoBehaviour

**Location:** `Assets/Behaviours/<Subcategory>/<Name>.cs`
**Namespace:** `Assembler.Behaviours.<Subcategory>`

Subcategories: `Movement`, `Physics`, `Camera`, `Sprites`, `Audio`, `Spawners`, `Debug`, `Debug.UI`,
`VariableUpdaters`, `ListOperations`, `Triggers.Input`, `Triggers.Timing`, `Triggers.Conditionals`,
`Triggers.Physical`.

### Regular behaviour

```csharp
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Movement
{
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

### Input trigger

```csharp
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input
{
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
["yaml key name"] = (YourInfo.Create, new[]
{
	new PropDescriptor("PropertyName", typeof(PropertyType)),
	new PropDescriptor("AnotherProp", typeof(AnotherType))
}),
```

### Rules

- The dictionary key is the **YAML behaviour name** — lowercase, with spaces (e.g. `"timer trigger"`, `"key hold trigger"`).
- The first tuple element is the Info's static `Create` method reference.
- The second element is a `PropDescriptor[]` — one per property. Use `Array.Empty<PropDescriptor>()` if no properties.
- `PropDescriptor` name strings must **exactly match** the strings used in `props.GetValueOrDefault("...")` in the Info's `Create` method.
- Common property types: `typeof(Vector3)`, `typeof(float)`, `typeof(int)`, `typeof(bool)`, `typeof(string)`, `typeof(Color)`, `typeof(Sprite)`, `typeof(AudioClip)`, `typeof(string[])`, `typeof(object[])`, `typeof(Dictionary<string, AssemblerValue>)`.

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

## Checklist

When adding a new behaviour, create/modify these 5 things in order:

1. `Assets/Parsing/Info/Behaviours/<Name>Info.cs` — Info record with `Create` and `SubstituteParameters`
2. `Assets/Resolving/Behaviours/<Name>Data.cs` — Data class with `IValueProvider<T>` properties
3. `Assets/Behaviours/<Subcategory>/<Name>.cs` — MonoBehaviour with `Execute()` override
4. `Assets/Parsing/BehaviourRegistry.cs` — Add entry to `All` dictionary
5. `Assets/Building/GameBehaviourFactory.cs` — Add entry to `Builders` dictionary

**Do not forget any step.** A missing registry or builder entry will cause a runtime error when the YAML
references the behaviour.
