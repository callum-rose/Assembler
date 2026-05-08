namespace Assembler.Parsing2;

public record Entity(
	string Id,
	IReadOnlyList<string> Tags,
	Vector3 InitialPosition,
	Vector3 InitialRotation,
	IReadOnlyList<Behaviour> Behaviours);