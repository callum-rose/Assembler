using System;
using System.Collections.Generic;
using Assembler.Parsing.Phase2.Info;

namespace Assembler.Parsing.Phase3
{
	public abstract class BehaviourData
	{
		public string Id { get; }
		public IReadOnlyList<Action> Listeners { get; }

		protected BehaviourData(string id, IReadOnlyList<Action> listeners)
		{
			Id = id;
			Listeners = listeners;
		}
	}

	public sealed class BoxColliderData : BehaviourData
	{
		public IValueProvider<UnityEngine.Vector3> Size { get; }
		public IValueProvider<bool> IsTrigger { get; }

		public BoxColliderData(string id, IReadOnlyList<Action> listeners, IValueProvider<UnityEngine.Vector3> size, IValueProvider<bool> isTrigger) :
			base(id, listeners) =>
			(Size, IsTrigger) = (size, isTrigger);
	}

	public sealed class SphereColliderData : BehaviourData
	{
		public IValueProvider<float> Radius { get; }
		public IValueProvider<bool> IsTrigger { get; init; } = NullValueProvider<bool>.Instance;

		public SphereColliderData(string id, IReadOnlyList<Action> listeners, IValueProvider<float> radius) : base(id, listeners) => Radius = radius;
	}

	public sealed class RigidbodyData : BehaviourData
	{
		public IValueProvider<bool> IsKinematic { get; init; } = NullValueProvider<bool>.Instance;
		public IValueProvider<bool> UseGravity { get; init; } = NullValueProvider<bool>.Instance;

		public RigidbodyData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}

	public sealed class VelocityData : BehaviourData
	{
		public IValueProvider<UnityEngine.Vector3> Velocity { get; }

		public VelocityData(string id, IReadOnlyList<Action> listeners, IValueProvider<UnityEngine.Vector3> velocity) : base(id, listeners) => Velocity = velocity;
	}

	public sealed class TranslateData : BehaviourData
	{
		public IValueProvider<UnityEngine.Vector3> Displacement { get; }

		public TranslateData(string id, IReadOnlyList<Action> listeners, IValueProvider<UnityEngine.Vector3> displacement) : base(id, listeners) =>
			Displacement = displacement;
	}

	public sealed class SetPositionData : BehaviourData
	{
		public IValueProvider<UnityEngine.Vector3> ValueExpression { get; }

		public SetPositionData(string id, IReadOnlyList<Action> listeners, IValueProvider<UnityEngine.Vector3> valueExpression) : base(id, listeners) =>
			ValueExpression = valueExpression;
	}

	public abstract class TriggerData : BehaviourData
	{
		public IReadOnlyList<Action> Listeners { get; }

		protected TriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) => Listeners = listeners;
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
			base(id, listeners)
		{
			Condition = condition;
		}
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

	public sealed class OnStartTriggerData : TriggerData
	{
		public OnStartTriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
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

		public WhenAllData(string id, IReadOnlyList<Action> listeners, IReadOnlyList<string> triggerIds) : base(id, listeners) => TriggerIds = triggerIds;
	}

	public sealed class WhenAnyData : BehaviourData
	{
		public IReadOnlyList<string> TriggerIds { get; }

		public WhenAnyData(string id, IReadOnlyList<Action> listeners, IReadOnlyList<string> triggerIds) : base(id, listeners) => TriggerIds = triggerIds;
	}

	public sealed class SpawnerData : BehaviourData
	{
		public IValueProvider<string> TemplateId { get; }
		public IValueProvider<UnityEngine.Vector3> Position { get; }

		public SpawnerData(
			string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<string> templateId,
			IValueProvider<UnityEngine.Vector3> position) : base(id, listeners) =>
			(TemplateId, Position) = (templateId, position);
	}

	public sealed class DestroyData : BehaviourData
	{
		public DestroyData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}

	public class VariableSetterData<T> : BehaviourData
	{
		public IValueProvider<T> ValueToSet { get; }
		public IValueProvider<T> ValueToGet { get; }

		public VariableSetterData(string id, IReadOnlyList<Action> listeners, IValueProvider<T> valueToSet, IValueProvider<T> valueToGet) : base(id, listeners) =>
			(ValueToSet, ValueToGet) = (valueToSet, valueToGet);
	}

	public class CameraData : BehaviourData
	{
		public IValueProvider<string> Perspective { get; }
		public IValueProvider<float> Size { get; }

		public CameraData(string id, IReadOnlyList<Action> listeners, IValueProvider<string> perspective, IValueProvider<float> size) : base(id, listeners) => (Perspective, Size) = (perspective, size);
	}
}