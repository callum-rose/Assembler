using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Phase2.Info
{
	public abstract record BehaviourInfo(string Id);

	public record BoxColliderInfo(string Id, ValueSource<Vector3> Size, ValueSource<bool> IsTrigger) : BehaviourInfo(Id);
	public record SphereColliderInfo(string Id, ValueSource<float> Radius, ValueSource<bool> IsTrigger) : BehaviourInfo(Id);
	public record RigidbodyInfo(string Id, ValueSource<bool> UseGravity) : BehaviourInfo(Id);
	public record VelocityInfo(string Id, ValueSource<Vector3> Velocity) : BehaviourInfo(Id);
	public record TranslateInfo(string Id, ValueSource<Vector3> Displacement) : BehaviourInfo(Id);
	public record KeyHoldTriggerInfo(string Id, ValueSource<string> Key, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record KeyDownTriggerInfo(string Id, ValueSource<string> Key, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record KeyUpTriggerInfo(string Id, ValueSource<string> Key, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record TapTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record DoubleTapTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record LongPressTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record SwipeTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record DragTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record PinchTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record RotateTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record ConditionInfo(string Id, ValueSource<string> ExpressionId, IReadOnlyList<ValueSource<object>> Arguments) : BehaviourInfo(Id);
	public record TimerTriggerInfo(string Id, ValueSource<float> Delay) : BehaviourInfo(Id);
	public record IntervalTriggerInfo(string Id, ValueSource<float> Interval) : BehaviourInfo(Id);
	public record EveryFrameTriggerInfo(string Id) : BehaviourInfo(Id);
	public record CollisionEnterTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record TriggerEnterTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record TriggerExitTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record TriggerStayTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record CollisionExitTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record CollisionStayTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record WhenAllInfo(string Id, IReadOnlyList<string> TriggerIds) : BehaviourInfo(Id);
	public record WhenAnyInfo(string Id, IReadOnlyList<string> TriggerIds) : BehaviourInfo(Id);
	public record SpawnerInfo(string Id, ValueSource<string> ObjectName, IReadOnlyList<string> Tags, ValueSource<Vector3> Position, ValueSource<Vector3> Rotation, IReadOnlyList<string> BehaviourIds) : BehaviourInfo(Id);
	public record VariableSetterInfo<T>(string Id, ValueSource<T> ValueToSet, ValueSource<T> ValueToGet) : BehaviourInfo(Id);
	public record SetPositionInfo(string Id, ValueSource<Vector3> ValueExpression) : BehaviourInfo(Id);
	public record CameraInfo(string Id, ValueSource<string> View, ValueSource<float> Size) : BehaviourInfo(Id);
	public record ConditionTriggerInfo(string Id, ValueSource<bool> Condition, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
}
