using System.Collections.Generic;

namespace Assembler.Parsing.Phase2.Parsing.Phase2.Info
{
	public abstract record BehaviourInfo(string Id);

	public record BoxColliderInfo(string Id, Vector3 Size, bool IsTrigger) : BehaviourInfo(Id);
	public record SphereColliderInfo(string Id, float Size) : BehaviourInfo(Id);
	public record RigidbodyInfo(string Id, bool UseGravity) : BehaviourInfo(Id);
	public record VelocityInfo(string Id, string VelocityVariableId) : BehaviourInfo(Id);
	public record TranslateInfo(string Id, Vector3 Displacement) : BehaviourInfo(Id);
	public record KeyHoldTriggerInfo(string Id, string Key, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
	public record KeyDownTriggerInfo(string Id, string Key, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
	public record KeyUpTriggerInfo(string Id, string Key, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
	public record TapTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
	public record DoubleTapTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
	public record LongPressTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
	public record SwipeTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
	public record DragTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
	public record PinchTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
	public record RotateTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
	public record ConditionInfo(string Id, string ExpressionId, IReadOnlyList<ValueInfo> Arguments) : BehaviourInfo(Id);
	public record AfterInfo(string Id, float Delay) : BehaviourInfo(Id);
	public record EveryInfo(string Id, float Interval) : BehaviourInfo(Id);
	public record EveryFrameInfo(string Id) : BehaviourInfo(Id);
	public record CollisionEnterTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
	public record TriggerEnterTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
	public record TriggerExitTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
	public record TriggerStayTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
	public record CollisionExitTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
	public record CollisionStayTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
	public record WhenAllInfo(string Id, IReadOnlyList<string> TriggerIds) : BehaviourInfo(Id);
	public record WhenAnyInfo(string Id, IReadOnlyList<string> TriggerIds) : BehaviourInfo(Id);
	public record SpawnerInfo(string Id, string ObjectName, IReadOnlyList<string> Tags, Vector3 Position, Vector3 Rotation, IReadOnlyList<string> BehaviourIds) : BehaviourInfo(Id);
	public record VectorVariableSetterInfo(string Id, string VariableId, string ExpressionId, IReadOnlyList<ValueInfo> Arguments) : BehaviourInfo(Id);
	public record IntVariableSetterInfo(string Id, string VariableId, string ExpressionId, IReadOnlyList<ValueInfo> Arguments) : BehaviourInfo(Id);
	public record FloatVariableSetterInfo(string Id, string VariableId, string ExpressionId, IReadOnlyList<ValueInfo> Arguments) : BehaviourInfo(Id);
	public record StringVariableSetterInfo(string Id, string VariableId, string ExpressionId, IReadOnlyList<ValueInfo> Arguments) : BehaviourInfo(Id);
	public record BoolVariableSetterInfo(string Id, string VariableId, string ExpressionId, IReadOnlyList<ValueInfo> Arguments) : BehaviourInfo(Id);
	public record SetPositionInfo(string Id, Vector3 ValueExpression) : BehaviourInfo(Id);
	public record CameraInfo(string Id, string View, float Size) : BehaviourInfo(Id);
	public record ConditionTriggerInfo(string Id, string ExpressionId, IReadOnlyList<ValueInfo> Arguments) : BehaviourInfo(Id);
}