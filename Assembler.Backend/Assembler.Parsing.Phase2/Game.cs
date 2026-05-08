namespace Assembler.Parsing2;

public record Game(
	Info Info,
	World World,
	Physics Physics,
	IReadOnlyList<Value> Constants,
	IReadOnlyList<Value> Variables,
	IReadOnlyList<Expression> Expressions,
	IReadOnlyList<Entity> Entities);