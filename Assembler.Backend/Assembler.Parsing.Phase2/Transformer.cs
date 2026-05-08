using Assembler.Parsing.Phase1.Dtos;

namespace Assembler.Parsing2;

public static class Transformer
{
	public static Game Transform(GameDto gameDto)
	{
		var info = new Info(gameDto.Game?.Title ?? string.Empty, gameDto.Game?.Description ?? string.Empty);
		var world = new World(gameDto.World?.Dimensionality ?? 0, gameDto.World?.BackgroundColor ?? string.Empty);
		var physics = new Physics(gameDto.Physics?.Gravity?.ToVector3([]) ?? new Vector3(0, 0, 0));

		var constants = new List<Value>(gameDto.Constants?.Count ?? 0);

		foreach (var valueDto in gameDto.Constants ?? [])
		{
			var value = new Value(valueDto.Id ?? string.Empty, Convert(constants, valueDto.Value));
			constants.Add(value);
		}

		var variables = new List<Value>(gameDto.Variables?.Count ?? 0);

		foreach (var valueDto in gameDto.Variables ?? [])
		{
			var value = new Value(valueDto.Id ?? string.Empty, Convert(constants, valueDto.Value));
			variables.Add(value);
		}

		IReadOnlyList<Value> allValues = [..constants, ..variables];

		var expressions = gameDto.Expressions?.Select(e => new Expression(e.Id ?? string.Empty,
			e.ArgumentTypes,
			e.ReturnType ?? string.Empty,
			e.Expression ?? string.Empty)).ToArray() ?? [];

		var entities = gameDto.Entities?.Select(e => new Entity(e.Id ?? string.Empty,
			e.Tags ?? [],
			ConvertVector(allValues, e.Position),
			ConvertVector(allValues, e.Rotation),
			e.Behaviours?.Select(b => CreateBehaviour(allValues, b)).ToArray() ?? [])).ToArray() ?? [];

		return new Game(info, world, physics, constants, variables, expressions, entities);
	}

	private static Behaviour CreateBehaviour(IReadOnlyList<Value> resolvedValues, BehaviourDto behaviourDto)
	{
		return behaviourDto.Type switch
		{
			"box collider" => new BoxCollider(behaviourDto.Id,
				ConvertVector(resolvedValues, behaviourDto.Properties["Size"]),
				ConvertGeneral<bool>(resolvedValues, behaviourDto.Properties["IsTrigger"])),
			_ => throw new InvalidOperationException($"Cannot convert behaviour type '{behaviourDto.Type}'")
		};
	}

	private static IReadOnlyDictionary<string, object> ConvertProperties(IReadOnlyList<Value> resolvedValues,
		IReadOnlyDictionary<string, object?>? properties) =>
		properties?.ToDictionary(p => p.Key, p => Convert(resolvedValues, p.Value)) ?? new Dictionary<string, object>();

	private static T ConvertGeneral<T>(IReadOnlyList<Value> resolvedValues, object? obj) =>
		obj switch
		{
			RefDto refDto => resolvedValues.FirstOrDefault(v => v.Id == refDto.Id) is T t
				? t
				: throw new Exception($"Cannot resolve reference '{refDto.Id}'"),
			T t => t,
			_ => throw new InvalidOperationException($"Cannot convert value '{obj}' of type '{obj?.GetType()}' to a {typeof(T)}")
		};

	private static Vector3 ConvertVector(IReadOnlyList<Value> resolvedValues, object? obj) =>
		obj switch
		{
			VecDto vecDto => vecDto.ToVector3(resolvedValues),
			RefDto refDto => (Vector3)(resolvedValues.FirstOrDefault(v => v.Id == refDto.Id)?.Object ?? throw new Exception(
				$"Cannot resolve reference '{refDto.Id}'")),
			null => new Vector3(0, 0, 0),
			_ => throw new InvalidOperationException($"Cannot convert {obj} to a Vector3")
		};

	private static object Convert(IReadOnlyList<Value> resolvedValues, object? obj) =>
		obj switch
		{
			VecDto vecDto => vecDto.ToVector3(resolvedValues),
			RefDto refDto => resolvedValues.FirstOrDefault(v => v.Id == refDto.Id)?.Object ?? throw new Exception(
				$"Cannot resolve reference '{refDto.Id}'"),
			not null => obj,
			_ => throw new InvalidOperationException($"Cannot convert {obj} to a value")
		};
}