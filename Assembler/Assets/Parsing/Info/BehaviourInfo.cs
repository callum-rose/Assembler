using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Parsing.Info
{
	public abstract record BehaviourInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners)
	{
		public abstract BehaviourInfo SubstituteParameters(
			IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues);
	}

	public record BoxColliderInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		ValueSource<Vector3> Size,
		ValueSource<bool> IsTrigger) : BehaviourInfo(Id, Listeners)
	{
		public static BoxColliderInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Size"), parameters: p),
				Transformer.Wrap<bool>(v, props?.GetValueOrDefault("IsTrigger"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new BoxColliderInfo(Id,
				substitutedListeners,
				Size.Substitute(parameters, allValues),
				IsTrigger.Substitute(parameters, allValues));
	}

	public record SphereColliderInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		ValueSource<float> Radius,
		ValueSource<bool> IsTrigger) : BehaviourInfo(Id, Listeners)
	{
		public static SphereColliderInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<float>(v, props?.GetValueOrDefault("Radius"), parameters: p),
				Transformer.Wrap<bool>(v, props?.GetValueOrDefault("IsTrigger"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new SphereColliderInfo(Id,
				substitutedListeners,
				Radius.Substitute(parameters, allValues),
				IsTrigger.Substitute(parameters, allValues));
	}

	public record RigidbodyInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<bool> UseGravity)
		: BehaviourInfo(Id, Listeners)
	{
		public static RigidbodyInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<bool>(v, props?.GetValueOrDefault("UseGravity"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new RigidbodyInfo(Id,
				substitutedListeners,
				UseGravity.Substitute(parameters, allValues));
	}

	public record VelocityInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<Vector3> Velocity)
		: BehaviourInfo(Id, Listeners)
	{
		public static VelocityInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Velocity"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new VelocityInfo(Id,
				substitutedListeners,
				Velocity.Substitute(parameters, allValues));
	}

	public record TranslateInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<Vector3> Displacement)
		: BehaviourInfo(Id, Listeners)
	{
		public static TranslateInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Displacement"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new TranslateInfo(Id,
				substitutedListeners,
				Displacement.Substitute(parameters, allValues));
	}

	public record KeyHoldTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<string> Key)
		: BehaviourInfo(Id, Listeners)
	{
		public static KeyHoldTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<string>(v, props?.GetValueOrDefault("Key"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new KeyHoldTriggerInfo(Id,
				substitutedListeners,
				Key.Substitute(parameters, allValues));
	}

	public record KeyDownTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<string> Key)
		: BehaviourInfo(Id, Listeners)
	{
		public static KeyDownTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<string>(v, props?.GetValueOrDefault("Key"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new KeyDownTriggerInfo(Id,
				substitutedListeners,
				Key.Substitute(parameters, allValues));
	}

	public record KeyUpTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<string> Key)
		: BehaviourInfo(Id, Listeners)
	{
		public static KeyUpTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<string>(v, props?.GetValueOrDefault("Key"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new KeyUpTriggerInfo(Id,
				substitutedListeners,
				Key.Substitute(parameters, allValues));
	}

	public record TapTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners)
	{
		public static TapTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id, listeners);

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new TapTriggerInfo(Id, substitutedListeners);
	}

	public record DoubleTapTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners)
	{
		public static DoubleTapTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id, listeners);

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new DoubleTapTriggerInfo(Id, substitutedListeners);
	}

	public record LongPressTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners)
	{
		public static LongPressTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id, listeners);

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new LongPressTriggerInfo(Id, substitutedListeners);
	}

	public record SwipeTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners)
	{
		public static SwipeTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id, listeners);

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new SwipeTriggerInfo(Id, substitutedListeners);
	}

	public record DragTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners)
	{
		public static DragTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id, listeners);

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new DragTriggerInfo(Id, substitutedListeners);
	}

	public record PinchTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners)
	{
		public static PinchTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id, listeners);

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new PinchTriggerInfo(Id, substitutedListeners);
	}

	public record RotateTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners)
	{
		public static RotateTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id, listeners);

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new RotateTriggerInfo(Id, substitutedListeners);
	}

	public record ConditionInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		ValueSource<string> ExpressionId,
		IReadOnlyList<ValueSource<object>> Arguments) : BehaviourInfo(Id, Listeners)
	{
		public static ConditionInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<string>(v, props?.GetValueOrDefault("ExpressionId"), parameters: p),
				Transformer.ConvertArgumentList(v, props?.GetValueOrDefault("Arguments")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new ConditionInfo(Id,
				substitutedListeners,
				ExpressionId.Substitute(parameters, allValues),
				Arguments.Select(a => a.Substitute(parameters, allValues)).ToArray());
	}

	public record TimerTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<float> Delay)
		: BehaviourInfo(Id, Listeners)
	{
		public static TimerTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<float>(v, props?.GetValueOrDefault("Delay"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new TimerTriggerInfo(Id,
				substitutedListeners,
				Delay.Substitute(parameters, allValues));
	}

	public record OnStartTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners)
	{
		public static OnStartTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id, listeners);

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new OnStartTriggerInfo(Id, substitutedListeners);
	}

	public record IntervalTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<float> Interval)
		: BehaviourInfo(Id, Listeners)
	{
		public static IntervalTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<float>(v, props?.GetValueOrDefault("Interval"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new IntervalTriggerInfo(Id,
				substitutedListeners,
				Interval.Substitute(parameters, allValues));
	}

	public record EveryFrameTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners)
	{
		public static EveryFrameTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id, listeners);

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new EveryFrameTriggerInfo(Id, substitutedListeners);
	}

	public record CollisionEnterTriggerInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		IReadOnlyList<string> TagsToDetect) : BehaviourInfo(Id, Listeners)
	{
		public static CollisionEnterTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.ConvertStringList(props?.GetValueOrDefault("TagsToDetect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new CollisionEnterTriggerInfo(Id, substitutedListeners, TagsToDetect);
	}

	public record TriggerEnterTriggerInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		IReadOnlyList<string> TagsToDetect) : BehaviourInfo(Id, Listeners)
	{
		public static TriggerEnterTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.ConvertStringList(props?.GetValueOrDefault("TagsToDetect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new TriggerEnterTriggerInfo(Id, substitutedListeners, TagsToDetect);
	}

	public record TriggerExitTriggerInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		IReadOnlyList<string> TagsToDetect) : BehaviourInfo(Id, Listeners)
	{
		public static TriggerExitTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.ConvertStringList(props?.GetValueOrDefault("TagsToDetect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new TriggerExitTriggerInfo(Id, substitutedListeners, TagsToDetect);
	}

	public record TriggerStayTriggerInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		IReadOnlyList<string> TagsToDetect) : BehaviourInfo(Id, Listeners)
	{
		public static TriggerStayTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.ConvertStringList(props?.GetValueOrDefault("TagsToDetect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new TriggerStayTriggerInfo(Id, substitutedListeners, TagsToDetect);
	}

	public record CollisionExitTriggerInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		IReadOnlyList<string> TagsToDetect) : BehaviourInfo(Id, Listeners)
	{
		public static CollisionExitTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.ConvertStringList(props?.GetValueOrDefault("TagsToDetect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new CollisionExitTriggerInfo(Id, substitutedListeners, TagsToDetect);
	}

	public record CollisionStayTriggerInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		IReadOnlyList<string> TagsToDetect) : BehaviourInfo(Id, Listeners)
	{
		public static CollisionStayTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.ConvertStringList(props?.GetValueOrDefault("TagsToDetect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new CollisionStayTriggerInfo(Id, substitutedListeners, TagsToDetect);
	}

	public record WhenAllInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, IReadOnlyList<string> TriggerIds)
		: BehaviourInfo(Id, Listeners)
	{
		public static WhenAllInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.ConvertStringList(props?.GetValueOrDefault("TriggerIds")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new WhenAllInfo(Id, substitutedListeners, TriggerIds);
	}

	public record WhenAnyInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, IReadOnlyList<string> TriggerIds)
		: BehaviourInfo(Id, Listeners)
	{
		public static WhenAnyInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.ConvertStringList(props?.GetValueOrDefault("TriggerIds")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new WhenAnyInfo(Id, substitutedListeners, TriggerIds);
	}

	public record SpawnerInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		ValueSource<string> TemplateId,
		ValueSource<Vector3> Position) : BehaviourInfo(Id, Listeners)
	{
		public static SpawnerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<string>(v, props?.GetValueOrDefault("TemplateId")),
				Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Position")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new SpawnerInfo(Id,
				substitutedListeners,
				TemplateId.Substitute(parameters, allValues),
				Position.Substitute(parameters, allValues));
	}

	public record DestroyInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners)
	{
		public static DestroyInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id, listeners);

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new DestroyInfo(Id, substitutedListeners);
	}

	public record VariableSetterInfo<T>(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		ValueSource<T> ValueToSet,
		ValueSource<T> ValueToGet) : BehaviourInfo(Id, Listeners)
	{
		public static VariableSetterInfo<T> Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<T>(v, props?.GetValueOrDefault("VariableId"), parameters: p),
				Transformer.Wrap<T>(v, props?.GetValueOrDefault("Value"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new VariableSetterInfo<T>(Id,
				substitutedListeners,
				ValueToSet.Substitute(parameters, allValues),
				ValueToGet.Substitute(parameters, allValues));
	}

	public record SetPositionInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<Vector3> ValueExpression)
		: BehaviourInfo(Id, Listeners)
	{
		public static SetPositionInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Position"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new SetPositionInfo(Id,
				substitutedListeners,
				ValueExpression.Substitute(parameters, allValues));
	}

	public record CameraInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		ValueSource<string> View,
		ValueSource<float> Size) : BehaviourInfo(Id, Listeners)
	{
		public static CameraInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<string>(v, props?.GetValueOrDefault("View"), parameters: p),
				Transformer.Wrap<float>(v, props?.GetValueOrDefault("Size"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new CameraInfo(Id,
				substitutedListeners,
				View.Substitute(parameters, allValues),
				Size.Substitute(parameters, allValues));
	}

	public record ConditionTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<bool> Condition)
		: BehaviourInfo(Id, Listeners)
	{
		public static ConditionTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<bool>(v, props?.GetValueOrDefault("Condition"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new ConditionTriggerInfo(Id,
				substitutedListeners,
				Condition.Substitute(parameters, allValues));
	}

	public record SpriteInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		ValueSource<Sprite> Sprite,
		ValueSource<Vector2> Size)
		: BehaviourInfo(Id, Listeners)
	{
		public static SpriteInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<Sprite>(v, props?.GetValueOrDefault("Sprite"), parameters: p),
				Transformer.Wrap<Vector2>(v, props?.GetValueOrDefault("Size"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new SpriteInfo(Id,
				substitutedListeners,
				Sprite.Substitute(parameters, allValues),
				Size.Substitute(parameters, allValues));
	}

	public record AudioSourceInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		ValueSource<AudioClip> Clip,
		ValueSource<bool> PlayOnStart,
		ValueSource<bool> Loop)
		: BehaviourInfo(Id, Listeners)
	{
		public static AudioSourceInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<AudioClip>(v, props?.GetValueOrDefault("Clip"), parameters: p),
				Transformer.Wrap<bool>(v, props?.GetValueOrDefault("PlayOnStart"), parameters: p),
				Transformer.Wrap<bool>(v, props?.GetValueOrDefault("Loop"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new AudioSourceInfo(Id,
				substitutedListeners,
				Clip.Substitute(parameters, allValues),
				PlayOnStart.Substitute(parameters, allValues),
				Loop.Substitute(parameters, allValues));
	}

	public record SphereGizmoInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		ValueSource<float> Radius,
		ValueSource<bool> IsWire,
		ValueSource<Color> Colour) : BehaviourInfo(Id, Listeners)
	{
		public static SphereGizmoInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<float>(v, props?.GetValueOrDefault("Radius"), parameters: p),
				Transformer.Wrap<bool>(v, props?.GetValueOrDefault("IsWire"), parameters: p),
				Transformer.Wrap<Color>(v, props?.GetValueOrDefault("Colour"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new SphereGizmoInfo(Id,
				substitutedListeners,
				Radius.Substitute(parameters, allValues),
				IsWire.Substitute(parameters, allValues),
				Colour.Substitute(parameters, allValues));
	}

	public record CubeGizmoInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		ValueSource<Vector3> Size,
		ValueSource<bool> IsWire,
		ValueSource<Color> Colour) : BehaviourInfo(Id, Listeners)
	{
		public static CubeGizmoInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Size"), parameters: p),
				Transformer.Wrap<bool>(v, props?.GetValueOrDefault("IsWire"), parameters: p),
				Transformer.Wrap<Color>(v, props?.GetValueOrDefault("Colour"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new CubeGizmoInfo(Id,
				substitutedListeners,
				Size.Substitute(parameters, allValues),
				IsWire.Substitute(parameters, allValues),
				Colour.Substitute(parameters, allValues));
	}
}