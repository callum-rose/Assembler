using Assembler.Parsing.Phase1.Dtos;

namespace Assembler.Parsing2;

public static class Transformer
{
	public static Game Transform(GameDto gameDto)
	{
		var info = new Info(gameDto.Game?.Title ?? string.Empty, gameDto.Game?.Description ?? string.Empty);
		var world = new World(gameDto.World?.Dimensionality ?? 0, gameDto.World?.BackgroundColor ?? string.Empty);
		var physics = new Physics(gameDto.Physics?.Gravity?.ToVector3(gameDto.Constants ?? []) ?? new Vector3(0, 0, 0));
		var constants = gameDto.Constants?.Select(c => new Value(c.Id ?? string.Empty, c.Value ?? null)).ToArray() ?? [];
		var variables = gameDto.Variables?.Select(v => new Value(v.Id ?? string.Empty, v.Value ?? null)).ToArray() ?? [];
		var expressions = gameDto.Expressions?.Select(e => new Expression(e.Id ?? string.Empty, e.Type ?? string.Empty, e.Expression ?? null)).ToArray() ?? [];
	}
}