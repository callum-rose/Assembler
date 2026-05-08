namespace Assembler.Parsing2;

public abstract record Behaviour(string Id);

public record BoxCollider(string Id, Vector3 Size, bool IsTrigger) : Behaviour(Id);
public record SphereCollider(string Id, float Size) : Behaviour(Id);
public record Rigidbody(string Id, bool UseGravity) : Behaviour(Id);
public record Velocity(string Id, string VelocityVariableId) : Behaviour(Id);
public record Translate(string Id, Vector3 Displacement) : Behaviour(Id);
public record KeyHoldTrigger(string Id, string Key, IReadOnlyList<Listener> Listeners) : Behaviour(Id);
public record CollisionEnterTrigger(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<Listener> Listeners) : Behaviour(Id);
public record TriggerEnterTrigger(string Id, IReadOnlyList<string> TagsToDetect, IReadOnlyList<Listener> Listeners) : Behaviour(Id);
public record VectorVariableSetter(string Id, string VariableId, string ExpressionId, IReadOnlyList<object> Arguments) : Behaviour(Id);
public record IntVariableSetter(string Id, string VariableId, string ExpressionId, IReadOnlyList<object> Arguments) : Behaviour(Id);
public record PositionSetter(string Id, Vector3 ValueExpression) : Behaviour(Id);
public record Camera(string Id, string View, float Size) : Behaviour(Id);
public record ConditionTrigger(string Id, string ExpressionId, IReadOnlyList<object> Arguments) : Behaviour(Id);