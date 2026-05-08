namespace Assembler.Parsing2;

public record Game(
	Info Info,
	World World,
	Physics Physics,
	IReadOnlyList<Value> Constants,
	IReadOnlyList<Value> Variables,
	IReadOnlyList<Expression> Expressions,
	IReadOnlyList<Entity> Entities);

public record Info(string Title, string Description);

public record World(int Dimensionality, string BackgroundColour);

public record Physics(Vector3 Gravity);

public record Value(string Id, object Object);

public record Entity(
	string Id,
	IReadOnlyList<string> Tags,
	Vector3 InitialPosition,
	Vector3 InitialRotation,
	IReadOnlyList<Behaviour> Behaviours);

public abstract record Behaviour(string Id);

public record BoxCollider(string Id, Vector3 Size, bool IsTrigger) : Behaviour(Id);

public record Expression(string Id, IReadOnlyList<string> ArgumentTypes, string ReturnType, string ExpressionBody);

