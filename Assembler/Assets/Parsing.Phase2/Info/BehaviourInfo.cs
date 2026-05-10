using System.Collections.Generic;

namespace Assembler.Parsing.Phase2.Info
{
	public abstract record BehaviourInfo(string Id);

	public record BoxColliderInfo(string Id, ValueWrapper<Vector3> Size, ValueWrapper<bool> IsTrigger) : BehaviourInfo(Id);
	public record SphereColliderInfo(string Id, ValueWrapper<float> Radius, ValueWrapper<bool> IsTrigger) : BehaviourInfo(Id);
	public record RigidbodyInfo(string Id, ValueWrapper<bool> UseGravity) : BehaviourInfo(Id);
	public record VelocityInfo(string Id, ValueWrapper<Vector3> Velocity) : BehaviourInfo(Id);
	public record TranslateInfo(string Id, ValueWrapper<Vector3> Displacement) : BehaviourInfo(Id);
	public record KeyHoldTriggerInfo(string Id, ValueWrapper<string> Key, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record KeyDownTriggerInfo(string Id, ValueWrapper<string> Key, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record KeyUpTriggerInfo(string Id, ValueWrapper<string> Key, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record TapTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record DoubleTapTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record LongPressTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record SwipeTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record DragTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record PinchTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record RotateTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record ConditionInfo(string Id, ValueWrapper<string> ExpressionId, IReadOnlyList<ValueWrapper<object>> Arguments) : BehaviourInfo(Id);
	public record TimerTriggerInfo(string Id, ValueWrapper<float> Delay) : BehaviourInfo(Id);
	public record IntervalTriggerInfo(string Id, ValueWrapper<float> Interval) : BehaviourInfo(Id);
	public record EveryFrameTriggerInfo(string Id) : BehaviourInfo(Id);
	public record CollisionEnterTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record TriggerEnterTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record TriggerExitTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record TriggerStayTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record CollisionExitTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record CollisionStayTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id);
	public record WhenAllInfo(string Id, IReadOnlyList<string> TriggerIds) : BehaviourInfo(Id);
	public record WhenAnyInfo(string Id, IReadOnlyList<string> TriggerIds) : BehaviourInfo(Id);
	public record SpawnerInfo(string Id, ValueWrapper<string> ObjectName, IReadOnlyList<string> Tags, ValueWrapper<Vector3> Position, ValueWrapper<Vector3> Rotation, IReadOnlyList<string> BehaviourIds) : BehaviourInfo(Id);
	public record VectorVariableSetterInfo(string Id, ValueWrapper<string> VariableId, ValueWrapper<string> ExpressionId, IReadOnlyList<ValueWrapper<object>> Arguments) : BehaviourInfo(Id);
	public record IntVariableSetterInfo(string Id, ValueWrapper<string> VariableId, ValueWrapper<string> ExpressionId, IReadOnlyList<ValueWrapper<object>> Arguments) : BehaviourInfo(Id);
	public record FloatVariableSetterInfo(string Id, ValueWrapper<string> VariableId, ValueWrapper<string> ExpressionId, IReadOnlyList<ValueWrapper<object>> Arguments) : BehaviourInfo(Id);
	public record StringVariableSetterInfo(string Id, ValueWrapper<string> VariableId, ValueWrapper<string> ExpressionId, IReadOnlyList<ValueWrapper<object>> Arguments) : BehaviourInfo(Id);
	public record BoolVariableSetterInfo(string Id, ValueWrapper<string> VariableId, ValueWrapper<string> ExpressionId, IReadOnlyList<ValueWrapper<object>> Arguments) : BehaviourInfo(Id);
	public record SetPositionInfo(string Id, ValueWrapper<Vector3> ValueExpression) : BehaviourInfo(Id);
	public record CameraInfo(string Id, ValueWrapper<string> View, ValueWrapper<float> Size) : BehaviourInfo(Id);
	public record ConditionTriggerInfo(string Id, ValueWrapper<string> ExpressionId, IReadOnlyList<ValueWrapper<object>> Arguments) : BehaviourInfo(Id);
}
