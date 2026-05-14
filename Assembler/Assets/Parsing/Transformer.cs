using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Deserialisation.Dtos;
using Assembler.Extensions;
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

			var assets = gameDto.Assets.EmptyIfNull().Select(a => a.Type switch
			{
				"sprite" => (AssetInfo)new SpriteAssetInfo(a.Id ?? string.Empty, a.Source ?? "resources", a.Path ?? string.Empty),
				"audioclip" => new AudioClipAssetInfo(a.Id ?? string.Empty, a.Source ?? "resources", a.Path ?? string.Empty),
				_ => throw new NotImplementedException($"Unknown asset type: {a.Type}")
			}).ToList();

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
				assets,
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

			return entry.Factory(id,
				GetListeners(behaviourDto, resolvedValues, parameters),
				behaviourDto.Properties,
				resolvedValues,
				parameters);
		}

		private static IReadOnlyList<BehaviourDescriptor> GetListeners(BehaviourDto behaviourDto,
			IReadOnlyList<VariableInfo> variables, IReadOnlyDictionary<string, object>? parameters) =>
			behaviourDto.Listeners?
				.Select(l => new BehaviourDescriptor(l.EntityId switch
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
			T? fallback = default, IReadOnlyDictionary<string, object>? parameters = null) =>
			raw switch
			{
				ParamRefDto paramRefDto when parameters is null => new ParameterSource<T>(paramRefDto.Id ?? string.Empty),
				ParamRefDto paramRefDto => !parameters.TryGetValue(paramRefDto.Id ?? string.Empty, out var paramValue)
					? throw new ParsingException($"Parameter '{paramRefDto.Id}' not found")
					: Wrap(resolvedValues, paramValue, fallback),
				ConstRefDto constRefDto => new ConstantSource<T>(constRefDto.ResolveValue<T>(resolvedValues)),
				AssetRefDto assetRefDto => new AssetSource<T>(assetRefDto.Id ?? string.Empty),
				VarRefDto varRefDto => new VariableSource<T>(varRefDto.Id ?? string.Empty),
				ExprRefDto exprRefDto => new ExpressionSource<T>(exprRefDto.ExpressionId ?? string.Empty,
					exprRefDto.Arguments
						.EmptyIfNull()
						.Select(a => Wrap<object>(resolvedValues, a, parameters: parameters)).ToArray()),
				VecDto vecDto when typeof(T) == typeof(Vector3) => new ConstantSource<T>(
					(T)(object)vecDto.ToVector3(resolvedValues)),
				VecDto vecDto when typeof(T) == typeof(Vector2) => new ConstantSource<T>(
					(T)(object)vecDto.ToVector2(resolvedValues)),
				VecDto vecDto => new ConstantSource<T>((T)(object)vecDto.ToVector3(resolvedValues)),
				null when fallback is not null => new ConstantSource<T>(fallback),
				null => None<T>.Instance,
				_ => new ConstantSource<T>(CoerceConstant<T>(raw))
			};

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