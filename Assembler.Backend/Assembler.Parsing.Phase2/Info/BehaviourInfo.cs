namespace Assembler.Parsing2.Info;

public abstract record BehaviourInfo(string Id);

public record BoxColliderInfo(string Id, ValueInfo<Vector3> Size, ValueInfo<bool> IsTrigger) : BehaviourInfo(Id);
public record SphereColliderInfo(string Id, float Size) : BehaviourInfo(Id);
public record RigidbodyInfo(string Id, bool UseGravity) : BehaviourInfo(Id);
public record VelocityInfo(string Id, string VelocityVariableId) : BehaviourInfo(Id);
public record TranslateInfo(string Id, Vector3 Displacement) : BehaviourInfo(Id);
public record KeyHoldTriggerInfo(string Id, string Key, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
public record CollisionEnterTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
public record TriggerEnterTriggerInfo(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id);
public record VectorVariableSetterInfo(string Id, string VariableId, string ExpressionId, IReadOnlyList<ValueInfo> Arguments) : BehaviourInfo(Id);
public record IntVariableSetterInfo(string Id, string VariableId, string ExpressionId, IReadOnlyList<ValueInfo> Arguments) : BehaviourInfo(Id);
public record SetPositionInfo(string Id, Vector3 ValueExpression) : BehaviourInfo(Id);
public record CameraInfo(string Id, string View, float Size) : BehaviourInfo(Id);
public record ConditionTriggerInfo(string Id, string ExpressionId, IReadOnlyList<ValueInfo> Arguments) : BehaviourInfo(Id);