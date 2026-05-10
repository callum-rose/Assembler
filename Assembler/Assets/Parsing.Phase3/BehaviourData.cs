using System;
using System.Collections.Generic;
using Assembler.Parsing.Phase2.Info;

namespace Assembler.Parsing.Phase3
{
	public abstract class BehaviourData
	{
		public string Id { get; }

		protected BehaviourData(string id) => Id = id;
	}

	public sealed class BoxColliderData : BehaviourData
	{
		public IValueProvider<UnityEngine.Vector3> Size { get; }
		public IValueProvider<bool> IsTrigger { get; }

		public BoxColliderData(string id, IValueProvider<UnityEngine.Vector3> size, IValueProvider<bool> isTrigger) :
			base(id) =>
			(Size, IsTrigger) = (size, isTrigger);
	}

	public sealed class SphereColliderData : BehaviourData
	{
		public IValueProvider<float> Radius { get; }
		public IValueProvider<bool> IsTrigger { get; init; } = NullValueProvider<bool>.Instance;

		public SphereColliderData(string id, IValueProvider<float> radius) : base(id) => Radius = radius;
	}

	public sealed class RigidbodyData : BehaviourData
	{
		public IValueProvider<bool> IsKinematic { get; init; } = NullValueProvider<bool>.Instance;
		public IValueProvider<bool> UseGravity { get; init; } = NullValueProvider<bool>.Instance;

		public RigidbodyData(string id) : base(id) { }
	}

	public sealed class VelocityData : BehaviourData
	{
		public IValueProvider<UnityEngine.Vector3> Velocity { get; }

		public VelocityData(string id, IValueProvider<UnityEngine.Vector3> velocity) : base(id) => Velocity = velocity;
	}

	public sealed class TranslateData : BehaviourData
	{
		public IValueProvider<UnityEngine.Vector3> Displacement { get; }

		public TranslateData(string id, IValueProvider<UnityEngine.Vector3> displacement) : base(id) =>
			Displacement = displacement;
	}

	public sealed class SetPositionData : BehaviourData
	{
		public IValueProvider<UnityEngine.Vector3> ValueExpression { get; }

		public SetPositionData(string id, IValueProvider<UnityEngine.Vector3> valueExpression) : base(id) =>
			ValueExpression = valueExpression;
	}

	public abstract class TriggerData : BehaviourData
	{
		public IReadOnlyList<Action> Listeners { get; }

		protected TriggerData(string id, IReadOnlyList<Action> listeners) : base(id) => Listeners = listeners;
	}

	public sealed class KeyHoldTriggerData : TriggerData
	{
		public IValueProvider<string> Key { get; }

		public KeyHoldTriggerData(string id, IValueProvider<string> key, IReadOnlyList<Action> listeners) :
			base(id, listeners) => Key = key;
	}

	public sealed class KeyDownTriggerData : TriggerData
	{
		public IValueProvider<string> Key { get; }

		public KeyDownTriggerData(string id, IValueProvider<string> key, IReadOnlyList<Action> listeners) :
			base(id, listeners) => Key = key;
	}

	public sealed class KeyUpTriggerData : TriggerData
	{
		public IValueProvider<string> Key { get; }

		public KeyUpTriggerData(string id, IValueProvider<string> key, IReadOnlyList<Action> listeners) : base(id,
			listeners) => Key = key;
	}

	public sealed class TapTriggerData : TriggerData
	{
		public TapTriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}

	public sealed class DoubleTapTriggerData : TriggerData
	{
		public DoubleTapTriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}

	public sealed class LongPressTriggerData : TriggerData
	{
		public LongPressTriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}

	public sealed class SwipeTriggerData : TriggerData
	{
		public SwipeTriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}

	public sealed class DragTriggerData : TriggerData
	{
		public DragTriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}

	public sealed class PinchTriggerData : TriggerData
	{
		public PinchTriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}

	public sealed class RotateTriggerData : TriggerData
	{
		public RotateTriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}

	public sealed class ConditionData : TriggerData
	{
		public IValueProvider<bool> Condition { get; }

		public ConditionData(string id, IValueProvider<bool> condition, IReadOnlyList<Action> listeners) :
			base(id, listeners) => Condition = condition;
	}

	public sealed class TimerTriggerData : TriggerData
	{
		public IValueProvider<float> Delay { get; }

		public TimerTriggerData(string id, IValueProvider<float> delay, IReadOnlyList<Action> listeners) :
			base(id, listeners) => Delay = delay;
	}

	public sealed class IntervalTriggerData : TriggerData
	{
		public IValueProvider<float> Interval { get; }
		public IValueProvider<int> Count { get; init; } = new ValueProvider<int>(0);

		public IntervalTriggerData(string id, IValueProvider<float> interval, IReadOnlyList<Action> listeners) : base(id,
			listeners) => Interval = interval;
	}

	public sealed class EveryFrameTriggerData : TriggerData
	{
		public EveryFrameTriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}

	public abstract class PhysicalTriggerData : TriggerData
	{
		public IReadOnlyList<string> TagsToDetect { get; }

		protected PhysicalTriggerData(string id, IReadOnlyList<string> tagsToDetect,
			IReadOnlyList<Action> listeners) : base(id, listeners) => TagsToDetect = tagsToDetect;
	}

	public sealed class CollisionEnterTriggerData : PhysicalTriggerData
	{
		public CollisionEnterTriggerData(string id, IReadOnlyList<string> tags, IReadOnlyList<Action> listeners) :
			base(id, tags, listeners) { }
	}

	public sealed class CollisionExitTriggerData : PhysicalTriggerData
	{
		public CollisionExitTriggerData(string id, IReadOnlyList<string> tags, IReadOnlyList<Action> listeners) :
			base(id, tags, listeners) { }
	}

	public sealed class CollisionStayTriggerData : PhysicalTriggerData
	{
		public CollisionStayTriggerData(string id, IReadOnlyList<string> tags, IReadOnlyList<Action> listeners) :
			base(id, tags, listeners) { }
	}

	public sealed class TriggerEnterTriggerData : PhysicalTriggerData
	{
		public TriggerEnterTriggerData(string id, IReadOnlyList<string> tags, IReadOnlyList<Action> listeners) :
			base(id, tags, listeners) { }
	}

	public sealed class TriggerExitTriggerData : PhysicalTriggerData
	{
		public TriggerExitTriggerData(string id, IReadOnlyList<string> tags, IReadOnlyList<Action> listeners) :
			base(id, tags, listeners) { }
	}

	public sealed class WhenAllData : BehaviourData
	{
		public IReadOnlyList<string> TriggerIds { get; }

		public WhenAllData(string id, IReadOnlyList<string> triggerIds) : base(id) => TriggerIds = triggerIds;
	}

	public sealed class WhenAnyData : BehaviourData
	{
		public IReadOnlyList<string> TriggerIds { get; }

		public WhenAnyData(string id, IReadOnlyList<string> triggerIds) : base(id) => TriggerIds = triggerIds;
	}

	public sealed class SpawnerData : BehaviourData
	{
		public BehaviourInfo Info { get; }

		public SpawnerData(string id, BehaviourInfo info) : base(id) => Info = info;
	}

	public class VariableSetterData<T> : BehaviourData
	{
		public IValueProvider<T> ValueProvider { get; }
		public ValueContainer<T> ValueContainer { get; }

		public VariableSetterData(string id, IValueProvider<T> valueProvider, ValueContainer<T> valueContainer) : base(id) =>
			(ValueProvider, ValueContainer) = (valueProvider, valueContainer);
	}
}