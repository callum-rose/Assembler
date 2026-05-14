using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Phase2.Info
{
	public abstract record BehaviourInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners);

	public record BoxColliderInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<Vector3> Size, ValueSource<bool> IsTrigger) : BehaviourInfo(Id, Listeners);
	public record SphereColliderInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<float> Radius, ValueSource<bool> IsTrigger) : BehaviourInfo(Id, Listeners);
	public record RigidbodyInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<bool> UseGravity) : BehaviourInfo(Id, Listeners);
	public record VelocityInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<Vector3> Velocity) : BehaviourInfo(Id, Listeners);
	public record TranslateInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<Vector3> Displacement) : BehaviourInfo(Id, Listeners);
	public record KeyHoldTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<string> Key) : BehaviourInfo(Id, Listeners);
	public record KeyDownTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<string> Key) : BehaviourInfo(Id, Listeners);
	public record KeyUpTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<string> Key) : BehaviourInfo(Id, Listeners);
	public record TapTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners);
	public record DoubleTapTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners);
	public record LongPressTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners);
	public record SwipeTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners);
	public record DragTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners);
	public record PinchTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners);
	public record RotateTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners);
	public record ConditionInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<string> ExpressionId, IReadOnlyList<ValueSource<object>> Arguments) : BehaviourInfo(Id, Listeners);
	public record TimerTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<float> Delay) : BehaviourInfo(Id, Listeners);
	public record OnStartTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners);
	public record IntervalTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<float> Interval) : BehaviourInfo(Id, Listeners);
	public record EveryFrameTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners);
	public record CollisionEnterTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, IReadOnlyList<string> TagsToDetect) : BehaviourInfo(Id, Listeners);
	public record TriggerEnterTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, IReadOnlyList<string> TagsToDetect) : BehaviourInfo(Id, Listeners);
	public record TriggerExitTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, IReadOnlyList<string> TagsToDetect) : BehaviourInfo(Id, Listeners);
	public record TriggerStayTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, IReadOnlyList<string> TagsToDetect) : BehaviourInfo(Id, Listeners);
	public record CollisionExitTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, IReadOnlyList<string> TagsToDetect) : BehaviourInfo(Id, Listeners);
	public record CollisionStayTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, IReadOnlyList<string> TagsToDetect) : BehaviourInfo(Id, Listeners);
	public record WhenAllInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, IReadOnlyList<string> TriggerIds) : BehaviourInfo(Id, Listeners);
	public record WhenAnyInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, IReadOnlyList<string> TriggerIds) : BehaviourInfo(Id, Listeners);
	public record SpawnerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<string> TemplateId, ValueSource<Vector3> Position) : BehaviourInfo(Id, Listeners);
	public record DestroyInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners);
	public record VariableSetterInfo<T>(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<T> ValueToSet, ValueSource<T> ValueToGet) : BehaviourInfo(Id, Listeners);
	public record SetPositionInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<Vector3> ValueExpression) : BehaviourInfo(Id, Listeners);
	public record CameraInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<string> View, ValueSource<float> Size) : BehaviourInfo(Id, Listeners);
	public record ConditionTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<bool> Condition) : BehaviourInfo(Id, Listeners);
}
