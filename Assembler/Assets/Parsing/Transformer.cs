using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Deserialisation.Dtos;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Parsing
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

			var templates = gameDto.Templates?
				.Select(t => new ConcreteEntityInfo(
					t.Id ?? string.Empty,
					NullEntityInfo.Instance,
					t.Tags ?? new List<string>(),
					Wrap<Vector3>(allValues, t.Position),
					Wrap<Vector3>(allValues, t.Rotation),
					t.Behaviours?.Select(b => CreateBehaviour(allValues, b)).ToArray() ??
					Array.Empty<BehaviourInfo>()))
				.ToArray() ?? Array.Empty<ConcreteEntityInfo>();

			var entities = gameDto.Entities?.Select(entityDto =>
				{
					EntityInfo template;
					Dictionary<string, object> parameters;

					var entityId = entityDto.Id ?? string.Empty;

					if (entityDto.Template is null)
					{
						template = NullEntityInfo.Instance;
						parameters = new Dictionary<string, object>();
					}
					else
					{
						template = templates.First(t => t.Id == entityDto.Template.Id);

						parameters = entityDto.Template.Parameters ?? new Dictionary<string, object>();
						parameters.Add("self_id", entityId);
					}

					var ownBehaviours = entityDto.Behaviours?.Select(b => CreateBehaviour(allValues, b, parameters))
					                    ?? Enumerable.Empty<BehaviourInfo>();

					var inheritedBehaviours = template is NullEntityInfo
						? Enumerable.Empty<BehaviourInfo>()
						: template.Behaviours.Select(b => TemplateInstantiator.SubstituteBehaviour(b, parameters, allValues));

					return new ConcreteEntityInfo(entityId,
						template,
						(entityDto.Tags ?? Enumerable.Empty<string>()).Concat(template.Tags).ToList(),
						Wrap<Vector3>(allValues, entityDto.Position, parameters: parameters),
						Wrap<Vector3>(allValues, entityDto.Rotation, parameters: parameters),
						inheritedBehaviours.Concat(ownBehaviours).ToArray());
				})
				.ToArray() ?? Array.Empty<EntityInfo>();

			var gameOverCondition = Wrap<bool>(allValues, gameDto.GameOverCondition);

			return new GameInfo(info,
				world,
				physics,
				variables,
				allValues,
				expressions,
				templates,
				entities,
				gameOverCondition);
		}

		private static BehaviourInfo CreateBehaviour(IReadOnlyList<VariableInfo> resolvedValues,
			BehaviourDto behaviourDto, IReadOnlyDictionary<string, object>? parameters = null)
		{
			var id = behaviourDto.Id ?? string.Empty;
			var type = behaviourDto.Type ?? string.Empty;

			if (!BehaviourRegistry.All.TryGetValue(type, out var entry))
			{
				throw new ParsingException($"Cannot convert behaviour type '{type}'");
			}

			return entry.Factory(id, GetListeners(behaviourDto, resolvedValues, parameters),
				behaviourDto.Properties, resolvedValues, parameters);
		}

		private static IReadOnlyList<BehaviourDescriptor> GetListeners(BehaviourDto behaviourDto,
			IReadOnlyList<VariableInfo> variables, IReadOnlyDictionary<string, object>? parameters) =>
			behaviourDto.Listeners
				?.Select(l => new BehaviourDescriptor(l.EntityId switch
					{
						ParamRefDto paramRefDto when parameters is null => ParameterEntityIdSentinel +
						                                                   (paramRefDto.Id ?? string.Empty),
						ParamRefDto paramRefDto => (string)parameters[paramRefDto.Id ?? string.Empty],
						ConstRefDto constDto => constDto.ResolveValue<string>(variables),
						string behaviourId => behaviourId,
						_ => throw new ParsingException($"Cannot get Id for listener {l.EntityId}")
					},
					l.BehaviourId ?? string.Empty)).ToArray() ??
			Array.Empty<BehaviourDescriptor>();

		internal const string ParameterEntityIdSentinel = "@param:";

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

		internal static IReadOnlyList<string> ConvertStringList(object? obj) =>
			obj is List<object> list
				? list.Select(item => item as string ?? item?.ToString() ?? string.Empty).ToArray()
				: Array.Empty<string>();

		internal static IReadOnlyList<ValueSource<object>> ConvertArgumentList(IReadOnlyList<VariableInfo> resolvedValues,
			object? obj) =>
			obj is List<object> list
				? list.Select(item => Wrap<object>(resolvedValues, item)).ToArray()
				: Array.Empty<ValueSource<object>>();

		/// <summary>
		/// Wraps a raw deserialised value into a <see cref="ValueSource{T}"/>.
		/// Constants (including <see cref="ConstRefDto"/>) are dereferenced to their values.
		/// Variable references become <see cref="VariableSource{T}"/>.
		/// Expression references become <see cref="ExpressionSource{T}"/> with their arguments
		/// recursively wrapped as <see cref="ValueSource{T}"/>.
		/// </summary>
		internal static ValueSource<T> Wrap<T>(IReadOnlyList<VariableInfo> resolvedValues, object? raw,
			T? fallback = default, IReadOnlyDictionary<string, object>? parameters = null)
		{
			switch (raw)
			{
				case ParamRefDto paramRefDto:
				{
					if (parameters == null)
					{
						return new ParameterSource<T>(paramRefDto.Id ?? string.Empty);
					}

					if (!parameters.TryGetValue(paramRefDto.Id ?? string.Empty, out var paramValue))
					{
						throw new ParsingException($"Parameter '{paramRefDto.Id}' not found");
					}

					return Wrap(resolvedValues, paramValue, fallback);
				}

				case ConstRefDto constRefDto:
					return new ConstantSource<T>(constRefDto.ResolveValue<T>(resolvedValues));

				case VarRefDto varRefDto:
					return new VariableSource<T>(varRefDto.Id ?? string.Empty);

				case ExprRefDto exprRefDto:
				{
					var args = exprRefDto.Arguments ?? Array.Empty<object>();
					var wrappedArgs = args.Select(a => Wrap<object>(resolvedValues, a, parameters: parameters)).ToArray();
					return new ExpressionSource<T>(exprRefDto.ExpressionId ?? string.Empty, wrappedArgs);
				}

				case VecDto vecDto when typeof(T) == typeof(Vector3):
					return new ConstantSource<T>((T)(object)vecDto.ToVector3(resolvedValues));

				case VecDto vecDto when typeof(T) == typeof(Vector2):
					return new ConstantSource<T>((T)(object)vecDto.ToVector2(resolvedValues));

				case VecDto vecDto:
					return new ConstantSource<T>((T)(object)vecDto.ToVector3(resolvedValues));

				case null when fallback is not null:
					return new ConstantSource<T>(fallback);

				case null:
					return None<T>.Instance;

				default:
					return new ConstantSource<T>(CoerceConstant<T>(raw));
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