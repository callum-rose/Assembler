using Assembler.Parsing.Phase1.Dtos;
using System.Collections.Generic;
using System.Linq;

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
		var id = behaviourDto.Id ?? string.Empty;
		var props = behaviourDto.Properties;

		return behaviourDto.Type switch
		{
			"box collider" => new BoxCollider(id,
				props?.GetValueOrDefault("Size") is { } boxSize
					? ConvertVector(resolvedValues, boxSize)
					: new Vector3(0, 0, 0),
				props?.GetValueOrDefault("IsTrigger") is bool isTrigger && isTrigger),

			"sphere collider" => new SphereCollider(id,
				ConvertFloat(resolvedValues, props?["Size"])),

			"rigidbody" => new Rigidbody(id,
				ConvertGeneral<bool>(resolvedValues, props?["UseGravity"])),

			"velocity" => new Velocity(id,
				ConvertGeneral<string>(resolvedValues, props?["VelocityVariableId"])),

			"translate" => new Translate(id,
				ConvertVector(resolvedValues, props?["Displacement"])),

			"key hold trigger" => new KeyHoldTrigger(id,
				ConvertGeneral<string>(resolvedValues, props?["Key"]),
				ConvertListeners(props?["Listeners"])),

			"collision enter trigger" => new CollisionEnterTrigger(id,
				ConvertStringList(props?["TagsToDetect"]),
				ConvertListeners(props?["Listeners"])),

			"trigger enter trigger" => new TriggerEnterTrigger(id,
				ConvertStringList(props?["TagsToDetect"]),
				ConvertListeners(props?["Listeners"])),

			"vector variable setter" => new VectorVariableSetter(id,
				ConvertGeneral<string>(resolvedValues, props?["VariableId"]),
				ConvertGeneral<string>(resolvedValues, props?["ExpressionId"]),
				ConvertArgumentList(resolvedValues, props?["Arguments"])),

			"int variable setter" => new IntVariableSetter(id,
				ConvertGeneral<string>(resolvedValues, props?["VariableId"]),
				ConvertGeneral<string>(resolvedValues, props?["ExpressionId"]),
				ConvertArgumentList(resolvedValues, props?["Arguments"])),

			"position setter" => new PositionSetter(id,
				ConvertVector(resolvedValues, props?["ValueExpression"])),

			"camera" => new Camera(id,
				ConvertGeneral<string>(resolvedValues, props?["View"]),
				ConvertFloat(resolvedValues, props?["Size"])),

			"condition trigger" => new ConditionTrigger(id,
				ConvertGeneral<string>(resolvedValues, props?["ExpressionId"]),
				ConvertArgumentList(resolvedValues, props?["Arguments"])),

			_ => throw new ParsingException($"Cannot convert behaviour type '{behaviourDto.Type}'")
		};
	}

	private static IReadOnlyList<Listener> ConvertListeners(object? obj) =>
		obj is List<object> list
			? list.Select(item =>
			{
				string? entityId = GetValueFromDictionary(item, "EntityId") as string;
				string? behaviourId = GetValueFromDictionary(item, "BehaviourId") as string;

				if (entityId is null || behaviourId is null)
				{
					throw new ParsingException($"Cannot convert listener: {item}. Missing EntityId or BehaviourId.");
				}

				return new Listener(entityId, behaviourId);
			}).ToArray()
			: [];

	private static object? GetValueFromDictionary(object item, string key) =>
		item is IDictionary<string, object> sd && sd.TryGetValue(key, out var val) ? val : null;

	private static IReadOnlyList<string> ConvertStringList(object? obj) =>
		obj is List<object> list
			? list.Select(item => item as string ?? item?.ToString() ?? string.Empty).ToArray()
			: [];

	private static IReadOnlyList<object> ConvertArgumentList(IReadOnlyList<Value> resolvedValues, object? obj) =>
		obj is List<object> list
			? list.Select(item => Convert(resolvedValues, item)).ToArray()
			: [];

	private static float ConvertFloat(IReadOnlyList<Value> resolvedValues, object? obj)
	{
		var value = Convert(resolvedValues, obj);

		return value switch
		{
			float f => f,
			int i => (float)i,
			double d => (float)d,
			_ => throw new ParsingException($"Cannot convert '{value}' to float")
		};
	}

	private static IReadOnlyDictionary<string, object> ConvertProperties(IReadOnlyList<Value> resolvedValues,
		IReadOnlyDictionary<string, object?>? properties) =>
		properties?.ToDictionary(p => p.Key, p => Convert(resolvedValues, p.Value)) ?? new Dictionary<string, object>();

	private static T ConvertGeneral<T>(IReadOnlyList<Value> resolvedValues, object? obj) =>
		obj switch
		{
			RefDto refDto => refDto.ResolveValue<T>(resolvedValues),
			T t => t,
			_ => throw new ParsingException($"Cannot convert value '{obj}' of type '{obj?.GetType()}' to a {typeof(T)}")
		};

	private static Vector3 ConvertVector(IReadOnlyList<Value> resolvedValues, object? obj) =>
		obj switch
		{
			VecDto vecDto => vecDto.ToVector3(resolvedValues),
			RefDto refDto => refDto.ResolveValue<Vector3>(resolvedValues),
			null => new Vector3(0, 0, 0),
			_ => throw new ParsingException($"Cannot convert {obj} to a Vector3")
		};

	private static object Convert(IReadOnlyList<Value> resolvedValues, object? obj) =>
		obj switch
		{
			VecDto vecDto => vecDto.ToVector3(resolvedValues),
			RefDto refDto => resolvedValues.FirstOrDefault(v => v.Id == refDto.Id)?.Object ?? throw new ParsingException(
				$"Cannot resolve reference '{refDto.Id}'"),
			not null => obj,
			_ => throw new ParsingException($"Cannot convert {obj} to a value")
		};
}