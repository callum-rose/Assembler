using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Parsing.Phase1.Dtos;
using Assembler.Parsing.Phase2.Info;

namespace Assembler.Parsing.Phase2
{
	public static class Transformer
	{
		public static GameInfo Transform(GameDto gameDto)
		{
			var info = new AboutInfo(gameDto.Game?.Title ?? string.Empty, gameDto.Game?.Description ?? string.Empty);
			var world = new WorldInfo(gameDto.World?.Dimensionality ?? 0, gameDto.World?.BackgroundColor ?? string.Empty);

			var physics =
				new PhysicsInfo(gameDto.Physics?.Gravity?.ToVector3(Array.Empty<VariableInfo>()) ?? new Vector3(0, 0, 0));

			var constants = new List<VariableInfo>(gameDto.Constants?.Count ?? 0);

			foreach (var valueDto in gameDto.Constants ?? Enumerable.Empty<ValueDto>())
			{
				var value = new VariableInfo(valueDto.Id ?? string.Empty, Convert(constants, valueDto.Value));
				constants.Add(value);
			}

			var variables = new List<VariableInfo>(gameDto.Variables?.Count ?? 0);

			foreach (var valueDto in gameDto.Variables ?? Enumerable.Empty<ValueDto>())
			{
				var value = new VariableInfo(valueDto.Id ?? string.Empty, Convert(constants, valueDto.Value));
				variables.Add(value);
			}

			IReadOnlyList<VariableInfo> allValues = constants.Concat(variables).ToArray();

			var expressions = gameDto.Expressions?.Select(e => new ExpressionInfo(e.Id ?? string.Empty,
				(e.ArgumentTypes ?? Array.Empty<string>())
				.Zip(e.ArgumentNames ?? Array.Empty<string>(), (type, name) => (type, name)).ToArray(),
				e.ReturnType ?? string.Empty,
				e.Expression ?? string.Empty)).ToArray();

			var entities = gameDto.Entities?.Select(e => new EntityInfo(e.Id ?? string.Empty,
					e.Tags ?? (IReadOnlyList<string>)Array.Empty<string>(),
					Wrap<Vector3>(allValues, e.Position),
					Wrap<Vector3>(allValues, e.Rotation),
					e.Behaviours?.Select(b => CreateBehaviour(allValues, b)).ToArray() ?? Array.Empty<BehaviourInfo>()))
				.ToArray() ?? Array.Empty<EntityInfo>();

			return new GameInfo(info, world, physics, variables, expressions, entities);
		}

		private static BehaviourInfo CreateBehaviour(IReadOnlyList<VariableInfo> resolvedValues, BehaviourDto behaviourDto)
		{
			var id = behaviourDto.Id ?? string.Empty;
			var props = behaviourDto.Properties;

			return behaviourDto.Type switch
			{
				"box collider" => new BoxColliderInfo(id,
					Wrap<Vector3>(resolvedValues, props?.GetValueOrDefault("Size")),
					Wrap<bool>(resolvedValues, props?.GetValueOrDefault("IsTrigger"))),

				"sphere collider" => new SphereColliderInfo(id,
					Wrap<float>(resolvedValues, props?.GetValueOrDefault("Radius")),
					Wrap<bool>(resolvedValues, props?.GetValueOrDefault("IsTrigger"))),

				"rigidbody" => new RigidbodyInfo(id,
					Wrap<bool>(resolvedValues, props?.GetValueOrDefault("UseGravity"))),

				"velocity" => new VelocityInfo(id, Wrap<Vector3>(resolvedValues, props?.GetValueOrDefault("Velocity"))),

				"translate" => new TranslateInfo(id, Wrap<Vector3>(resolvedValues, props?.GetValueOrDefault("Displacement"))),

				"key hold trigger" => new KeyHoldTriggerInfo(id,
					Wrap<string>(resolvedValues, props?.GetValueOrDefault("Key")),
					ConvertListeners(props?.GetValueOrDefault("Listeners"))),

				"collision enter trigger" => new CollisionEnterTriggerInfo(id,
					ConvertStringList(props?.GetValueOrDefault("TagsToDetect")),
					ConvertListeners(props?.GetValueOrDefault("Listeners"))),

				"trigger enter trigger" => new TriggerEnterTriggerInfo(id,
					ConvertStringList(props?.GetValueOrDefault("TagsToDetect")),
					ConvertListeners(props?.GetValueOrDefault("Listeners"))),

				"vector variable setter" => new VectorVariableSetterInfo(id,
					Wrap<string>(resolvedValues, props?.GetValueOrDefault("VariableId")),
					Wrap<string>(resolvedValues, props?.GetValueOrDefault("ExpressionId")),
					ConvertArgumentList(resolvedValues, props?.GetValueOrDefault("Arguments"))),

				"int variable setter" => new IntVariableSetterInfo(id,
					Wrap<string>(resolvedValues, props?.GetValueOrDefault("VariableId")),
					Wrap<string>(resolvedValues, props?.GetValueOrDefault("ExpressionId")),
					ConvertArgumentList(resolvedValues, props?.GetValueOrDefault("Arguments"))),

				"position setter" => new SetPositionInfo(id,
					Wrap<Vector3>(resolvedValues, props?.GetValueOrDefault("Position"))),

				"camera" => new CameraInfo(id,
					Wrap<string>(resolvedValues, props?.GetValueOrDefault("View")),
					Wrap<float>(resolvedValues, props?.GetValueOrDefault("Size"))),

				"condition trigger" => new ConditionTriggerInfo(id,
					Wrap<string>(resolvedValues, props?.GetValueOrDefault("ExpressionId")),
					ConvertArgumentList(resolvedValues, props?.GetValueOrDefault("Arguments"))),

				_ => throw new ParsingException($"Cannot convert behaviour type '{behaviourDto.Type}'")
			};
		}

		private static IReadOnlyList<BehaviourDescriptor> ConvertListeners(object? obj) =>
			obj is List<object> list
				? list.Select(item =>
				{
					string? entityId = GetValueFromDictionary(item, "EntityId") as string;
					string? behaviourId = GetValueFromDictionary(item, "BehaviourId") as string;

					if (entityId is null || behaviourId is null)
					{
						throw new ParsingException($"Cannot convert listener: {item}. Missing EntityId or BehaviourId.");
					}

					return new BehaviourDescriptor(entityId, behaviourId);
				}).ToArray()
				: (IReadOnlyList<BehaviourDescriptor>)Array.Empty<object>();

		private static object? GetValueFromDictionary(object item, string key) =>
			item is IDictionary<string, object> sd && sd.TryGetValue(key, out var val) ? val : null;

		private static IReadOnlyList<string> ConvertStringList(object? obj) =>
			obj is List<object> list
				? list.Select(item => item as string ?? item?.ToString() ?? string.Empty).ToArray()
				: Array.Empty<string>();

		private static IReadOnlyList<ValueWrapper<object>> ConvertArgumentList(IReadOnlyList<VariableInfo> resolvedValues,
			object? obj) =>
			obj is List<object> list
				? list.Select(item => Wrap<object>(resolvedValues, item)).ToArray()
				: Array.Empty<ValueWrapper<object>>();

		/// <summary>
		/// Wraps a raw deserialised value into a <see cref="ValueWrapper{T}"/>.
		/// Constants (including <see cref="ConstRefDto"/>) are dereferenced to their values.
		/// Variable references become <see cref="VariableRef{T}"/>.
		/// Expression references become <see cref="ExpressionRef{T}"/> with their arguments
		/// recursively wrapped as <see cref="ValueWrapper{T}"/>.
		/// </summary>
		private static ValueWrapper<T> Wrap<T>(IReadOnlyList<VariableInfo> resolvedValues, object? raw)
		{
			switch (raw)
			{
				case ConstRefDto constRefDto:
					return new Constant<T>(constRefDto.ResolveValue<T>(resolvedValues));

				case VarRefDto varRefDto:
					return new VariableRef<T>(varRefDto.Id ?? string.Empty);

				case ExprRefDto exprRefDto:
				{
					var args = exprRefDto.Arguments ?? Array.Empty<object>();
					var wrappedArgs = args.Select(a => Wrap<object>(resolvedValues, a)).ToArray();
					return new ExpressionRef<T>(exprRefDto.ExpressionId ?? string.Empty, wrappedArgs);
				}

				case VecDto vecDto when typeof(T) == typeof(Vector3):
					return new Constant<T>((T)(object)vecDto.ToVector3(resolvedValues));

				case VecDto vecDto when typeof(T) == typeof(Vector2):
					return new Constant<T>((T)(object)vecDto.ToVector2(resolvedValues));

				case null:
					return None<T>.Instance;

				default:
					return new Constant<T>(CoerceConstant<T>(raw));
			}
		}

		private static T CoerceConstant<T>(object value)
		{
			if (value is T t)
			{
				return t;
			}

			if (typeof(T) == typeof(float))
			{
				switch (value)
				{
					case int i:
						return (T)(object)(float)i;
					case double d:
						return (T)(object)(float)d;
				}
			}

			if (typeof(T) == typeof(object))
			{
				return (T)value;
			}

			throw new ParsingException($"Cannot convert value '{value}' of type '{value.GetType()}' to a {typeof(T)}");
		}

		// private static ValueWrapper<Vector3> ConvertVector(IReadOnlyList<VariableInfo> resolvedValues, object? obj) =>
		// 	obj switch
		// 	{
		// 		VecDto vecDto => vecDto.ToVector3(resolvedValues),
		// 		RefDto refDto => refDto.ResolveValue<Vector3>(resolvedValues),
		// 		null => new Vector3(0, 0, 0),
		// 		_ => throw new ParsingException($"Cannot convert {obj} to a Vector3")
		// 	};

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
}