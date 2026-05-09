using Assembler.Parsing.Phase1.Dtos;
using Assembler.Parsing2.Info;

namespace Assembler.Parsing2;

public static class Transformer
{
	public static GameInfo Transform(GameDto gameDto)
	{
		var info = new AboutInfo(gameDto.Game?.Title ?? string.Empty, gameDto.Game?.Description ?? string.Empty);
		var world = new WorldInfo(gameDto.World?.Dimensionality ?? 0, gameDto.World?.BackgroundColor ?? string.Empty);
		var physics = new PhysicsInfo(gameDto.Physics?.Gravity?.ToVector3([]) ?? new Vector3(0, 0, 0));

		var constants = new List<VariableInfo>(gameDto.Constants?.Count ?? 0);

		foreach (var valueDto in gameDto.Constants ?? [])
		{
			var value = new VariableInfo(valueDto.Id ?? string.Empty, Convert(constants, valueDto.Value));
			constants.Add(value);
		}

		var variables = new List<VariableInfo>(gameDto.Variables?.Count ?? 0);

		foreach (var valueDto in gameDto.Variables ?? [])
		{
			var value = new VariableInfo(valueDto.Id ?? string.Empty, Convert(constants, valueDto.Value));
			variables.Add(value);
		}

		IReadOnlyList<VariableInfo> allValues = [..constants, ..variables];

		var expressions = gameDto.Expressions?.Select(e => new ExpressionInfo(e.Id ?? string.Empty,
			(e.ArgumentTypes ?? []).Zip(e.ArgumentNames ?? [], (type, name) => (type, name)).ToArray(),
			e.ReturnType ?? string.Empty,
			e.Expression ?? string.Empty)).ToArray() ?? [];

		var entities = gameDto.Entities?.Select(e => new EntityInfo(e.Id ?? string.Empty,
			e.Tags ?? [],
			ConvertVector(allValues, e.Position),
			ConvertVector(allValues, e.Rotation),
			e.Behaviours?.Select(b => CreateBehaviour(allValues, b)).ToArray() ?? [])).ToArray() ?? [];

		return new GameInfo(info, world, physics, variables, expressions, entities);
	}

	private static BehaviourInfo CreateBehaviour(IReadOnlyList<VariableInfo> resolvedValues, BehaviourDto behaviourDto)
	{
		var id = behaviourDto.Id ?? string.Empty;
		var props = behaviourDto.Properties;

		return behaviourDto.Type switch
		{
			"box collider" => new BoxColliderInfo(id,
				props?.GetValueOrDefault("Size") is { } boxSize
					? ConvertVector(resolvedValues, boxSize)
					: new Vector3(0, 0, 0),
				props?.GetValueOrDefault("IsTrigger") is bool isTrigger && isTrigger),

			"sphere collider" => new SphereColliderInfo(id,
				ConvertFloat(resolvedValues, props?["Size"])),

			"rigidbody" => new RigidbodyInfo(id,
				ConvertGeneral<bool>(resolvedValues, props?["UseGravity"])),

			"velocity" => new VelocityInfo(id,
				ConvertGeneral<string>(resolvedValues, props?["VelocityVariableId"])),

			"translate" => new TranslateInfo(id,
				ConvertVector(resolvedValues, props?["Displacement"])),

			"key hold trigger" => new KeyHoldTriggerInfo(id,
				ConvertGeneral<string>(resolvedValues, props?["Key"]),
				ConvertListeners(props?["Listeners"])),

			"collision enter trigger" => new CollisionEnterTriggerInfo(id,
				ConvertStringList(props?["TagsToDetect"]),
				ConvertListeners(props?["Listeners"])),

			"trigger enter trigger" => new TriggerEnterTriggerInfo(id,
				ConvertStringList(props?["TagsToDetect"]),
				ConvertListeners(props?["Listeners"])),

			"vector variable setter" => new VectorVariableSetterInfo(id,
				ConvertGeneral<string>(resolvedValues, props?["VariableId"]),
				ConvertGeneral<string>(resolvedValues, props?["ExpressionId"]),
				ConvertArgumentList(resolvedValues, props?["Arguments"])),

			"int variable setter" => new IntVariableSetterInfo(id,
				ConvertGeneral<string>(resolvedValues, props?["VariableId"]),
				ConvertGeneral<string>(resolvedValues, props?["ExpressionId"]),
				ConvertArgumentList(resolvedValues, props?["Arguments"])),

			"position setter" => new SetPositionInfo(id,
				ConvertVector(resolvedValues, props?["ValueExpression"])),

			"camera" => new CameraInfo(id,
				ConvertGeneral<string>(resolvedValues, props?["View"]),
				ConvertFloat(resolvedValues, props?["Size"])),

			"condition trigger" => new ConditionTriggerInfo(id,
				ConvertGeneral<string>(resolvedValues, props?["ExpressionId"]),
				ConvertArgumentList(resolvedValues, props?["Arguments"])),

			_ => throw new ParsingException($"Cannot convert behaviour type '{behaviourDto.Type}'")
		};
	}

	private static IReadOnlyList<ListenerInfo> ConvertListeners(object? obj) =>
		obj is List<object> list
			? list.Select(item =>
			{
				string? entityId = GetValueFromDictionary(item, "EntityId") as string;
				string? behaviourId = GetValueFromDictionary(item, "BehaviourId") as string;

				if (entityId is null || behaviourId is null)
				{
					throw new ParsingException($"Cannot convert listener: {item}. Missing EntityId or BehaviourId.");
				}

				return new ListenerInfo(entityId, behaviourId);
			}).ToArray()
			: [];

	private static object? GetValueFromDictionary(object item, string key) =>
		item is IDictionary<string, object> sd && sd.TryGetValue(key, out var val) ? val : null;

	private static IReadOnlyList<string> ConvertStringList(object? obj) =>
		obj is List<object> list
			? list.Select(item => item as string ?? item?.ToString() ?? string.Empty).ToArray()
			: [];

	private static IReadOnlyList<ValueInfo> ConvertArgumentList(IReadOnlyList<VariableInfo> resolvedValues, object? obj) =>
		obj is List<object> list
			? list.Select(item => ConvertArgument(resolvedValues, item)).ToArray()
			: [];

	private static ValueInfo ConvertArgument(IReadOnlyList<VariableInfo> resolvedValues, object argument)
	{
		return argument switch
		{
			VarRefDto varRefDto => new VariableRefValueInfo(varRefDto.Id ?? string.Empty),
			ConstRefDto constRefDto => new ConstantValueInfo(resolvedValues.First(v => v.Id == constRefDto.Id).Value),
			not null => new ConstantValueInfo(Convert(resolvedValues, argument)),
			_ => throw new ParsingException($"Cannot convert argument: {argument}")
		};
	}

	private static float ConvertFloat(IReadOnlyList<VariableInfo> resolvedValues, object? obj)
	{
		var value = Convert(resolvedValues, obj);

		return value switch
		{
			float f => f,
			int i => i,
			double d => (float)d,
			_ => throw new ParsingException($"Cannot convert '{value}' to float")
		};
	}

	private static IReadOnlyDictionary<string, object> ConvertProperties(IReadOnlyList<VariableInfo> resolvedValues,
		IReadOnlyDictionary<string, object?>? properties) =>
		properties?.ToDictionary(p => p.Key, p => Convert(resolvedValues, p.Value)) ?? new Dictionary<string, object>();

	private static T ConvertGeneral<T>(IReadOnlyList<VariableInfo> resolvedValues, object? obj) =>
		obj switch
		{
			RefDto refDto => refDto.ResolveValue<T>(resolvedValues),
			T t => t,
			_ => throw new ParsingException($"Cannot convert value '{obj}' of type '{obj?.GetType()}' to a {typeof(T)}")
		};

	private static Vector3 ConvertVector(IReadOnlyList<VariableInfo> resolvedValues, object? obj) =>
		obj switch
		{
			VecDto vecDto => vecDto.ToVector3(resolvedValues),
			RefDto refDto => refDto.ResolveValue<Vector3>(resolvedValues),
			null => new Vector3(0, 0, 0),
			_ => throw new ParsingException($"Cannot convert {obj} to a Vector3")
		};

	private static object Convert(IReadOnlyList<VariableInfo> resolvedValues, object? obj) =>
		obj switch
		{
			VecDto vecDto => vecDto.ToVector3(resolvedValues),
			RefDto refDto => resolvedValues.FirstOrDefault(v => v.Id == refDto.Id)?.Value ?? throw new ParsingException(
				$"Cannot resolve reference '{refDto.Id}'"),
			not null => obj,
			_ => throw new ParsingException($"Cannot convert {obj} to a value")
		};
}