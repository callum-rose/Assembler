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

			var world = new WorldInfo(gameDto.World?.Dimensionality ?? 0,
				gameDto.World?.BackgroundColor?.ToColor(Array.Empty<ValueInfo>()) ?? Color.black);

			var physics =
				new PhysicsInfo(gameDto.Physics?.Gravity?.ToVector3(Array.Empty<ValueInfo>()) ?? new Vector3(0, 0, 0));

			var assets = gameDto.Assets.EmptyIfNull().Select(a => a.Type switch
			{
				"sprite" => (AssetInfo)new SpriteAssetInfo(a.Id ?? string.Empty,
					a.Source ?? "resources",
					a.Path ?? string.Empty),
				"audioclip" => new AudioClipAssetInfo(a.Id ?? string.Empty, a.Source ?? "resources", a.Path ?? string.Empty),
				_ => throw new NotImplementedException($"Unknown asset type: {a.Type}")
			}).ToList();

			var values = new List<ValueInfo>((gameDto.Constants?.Count ?? 0) + (gameDto.Variables?.Count ?? 0));

			foreach (var valueDto in gameDto.Constants.EmptyIfNull().Concat(gameDto.Variables.EmptyIfNull()))
			{
				var value = new ValueInfo(valueDto.Id ?? string.Empty, Convert(values, valueDto.Value));
				values.Add(value);
			}

			var expressions = gameDto.Expressions?.Select(e => new ExpressionInfo(e.Id ?? string.Empty,
				(e.ArgumentTypes ?? Array.Empty<string>())
				.Zip(e.ArgumentNames ?? Array.Empty<string>(), (type, name) => (type, name)).ToArray(),
				e.ReturnType ?? string.Empty,
				e.RegisterTypes ?? Array.Empty<string>(),
				e.RegisterTypeStatics ?? Array.Empty<string>(),
				e.Expression ?? string.Empty)).ToArray();

			var templates = gameDto.Templates?
				.Select(t => new ConcreteEntityInfo(
					t.Id ?? string.Empty,
					t.Tags ?? new List<string>(),
					CreateValueSource<Vector3>(values, ToAssemblerValue(t.Position)),
					CreateValueSource<Vector3>(values, ToAssemblerValue(t.Rotation)),
					t.Behaviours.EmptyIfNull().Select(b => CreateBehaviour(values, b, new Dictionary<string, AssemblerValue>())).ToArray()))
				.ToArray() ?? Array.Empty<ConcreteEntityInfo>();

			var entities = gameDto.Entities.EmptyIfNull().Select(CreateEntityInfo).ToArray();

			var gameOverCondition = CreateValueSource<bool>(values, ToAssemblerValue(gameDto.GameOverCondition));

			return new GameInfo(info,
				world,
				physics,
				assets,
				values,
				expressions,
				templates,
				entities,
				gameOverCondition);

			ConcreteEntityInfo CreateEntityInfo(EntityDto entityDto)
			{
				EntityInfo template;
				Dictionary<string, AssemblerValue> parameters;

				var entityId = entityDto.Id ?? string.Empty;

				if (entityDto.Template is null)
				{
					template = NullEntityInfo.Instance;
					parameters = new Dictionary<string, AssemblerValue>();
				}
				else
				{
					template = templates.First(t => t.Id == entityDto.Template.Id);
					parameters = ConvertProps(entityDto.Template.Parameters);
				}

				var ownBehaviours = entityDto.Behaviours.EmptyIfNull().Select(b => CreateBehaviour(values, b, parameters));
				
				return TemplateInstantiator.Instantiate(template,
					entityId,
					values,
					CreateValueSource<Vector3>(values, ToAssemblerValue(entityDto.Position), parameters: parameters),
					CreateValueSource<Vector3>(values, ToAssemblerValue(entityDto.Rotation), parameters: parameters),
					parameters,
					entityDto.Tags,
					ownBehaviours);
			}
		}

		private static BehaviourInfo CreateBehaviour(IReadOnlyList<ValueInfo> resolvedValues,
			BehaviourDto behaviourDto,
			IReadOnlyDictionary<string, AssemblerValue> parameters)
		{
			var id = behaviourDto.Id ?? string.Empty;
			var type = behaviourDto.Type ?? string.Empty;

			if (!BehaviourRegistry.All.TryGetValue(type, out var entry))
			{
				throw new ParsingException($"Cannot convert behaviour type '{type}'");
			}

			var props = ConvertProps(behaviourDto.Properties);

			var info = entry.Factory(id,
				GetListeners(behaviourDto, resolvedValues, parameters),
				props,
				resolvedValues,
				parameters);

			return behaviourDto.Tags is { Count: > 0 }
				? info with
				{
					Tags = behaviourDto.Tags.ToArray()
				}
				: info;
		}

		private static IReadOnlyList<ListenerInfo> GetListeners(BehaviourDto behaviourDto,
			IReadOnlyList<ValueInfo> variables,
			IReadOnlyDictionary<string, AssemblerValue> parameters) =>
			behaviourDto.Listeners
				.EmptyIfNull()
				.Select(l =>
				{
					if (l.EntityTag != null || l.BehaviourTag != null)
					{
						return new ListenerInfo(new BehaviourDescriptor(string.Empty, l.BehaviourId ?? string.Empty))
						{
							OutputMapping = l.Outputs ?? new Dictionary<string, string>(),
							EntityTag = l.EntityTag,
							BehaviourTag = l.BehaviourTag
						};
					}

					var entityId = l.EntityId switch
					{
						ParamRefDto paramRefDto => parameters.TryGetValue(paramRefDto.Id ?? string.Empty, out var pv)
						                           && pv is StringValue sv
							? sv.Value
							: ParameterEntityIdSentinel + (paramRefDto.Id ?? string.Empty),
						VarRefDto varRefDto => varRefDto.ResolveValue<string>(variables),
						string behaviourId => behaviourId,
						_ => throw new ParsingException($"Cannot get Id for listener {l.EntityId}")
					};

					var behaviourDescriptor = new BehaviourDescriptor(entityId, l.BehaviourId ?? string.Empty);

					return new ListenerInfo(behaviourDescriptor)
					{
						OutputMapping = l.Outputs ?? new Dictionary<string, string>()
					};
				})
				.ToArray();

		internal const string ParameterEntityIdSentinel = "@param:";

		internal static IReadOnlyList<string> ConvertStringList(AssemblerValue? value) =>
			value is ListValue list
				? list.Value
					.Select(item => item is StringValue sv ? sv.Value : item?.ToString() ?? string.Empty)
					.ToArray()
				: Array.Empty<string>();

		internal static IReadOnlyList<ValueSource<object>> ConvertArgumentList(IReadOnlyList<ValueInfo> resolvedValues,
			AssemblerValue? value) =>
			value is ListValue list
				? list.Value.Select(item =>
					CreateValueSource<object>(resolvedValues, item, new Dictionary<string, AssemblerValue>())).ToArray()
				: Array.Empty<ValueSource<object>>();

		internal static ValueSource<T> CreateValueSource<T>(IReadOnlyList<ValueInfo> resolvedValues,
			AssemblerValue raw,
			T? fallback = default) =>
			CreateValueSource(resolvedValues, raw, new Dictionary<string, AssemblerValue>(), fallback);

		/// <summary>
		/// Wraps a parsed <see cref="AssemblerValue"/> into a <see cref="ValueSource{T}"/>.
		/// Constants are dereferenced to their values.
		/// Variable references become <see cref="ValueReferenceSource{T}"/>.
		/// Expression references become <see cref="ExpressionSource{T}"/> with their arguments
		/// recursively wrapped as <see cref="ValueSource{T}"/>.
		/// </summary>
		internal static ValueSource<T> CreateValueSource<T>(IReadOnlyList<ValueInfo> resolvedValues,
			AssemblerValue raw,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			T? fallback = default) =>
			raw switch
			{
				ParamRef paramRef => parameters.TryGetValue(paramRef.Id, out var paramValue)
					? CreateValueSource(resolvedValues, paramValue, fallback)
					: new ParameterSource<T>(paramRef.Id),
				AssetRef assetRef => new AssetSource<T>(assetRef.Id),
				OutputRef outputRef => new TriggerOutputSource<T>(outputRef.Id),
				VarRef varRef => new ValueReferenceSource<T>(varRef.Id),
				ExprRef exprRef => new ExpressionSource<T>(exprRef.ExpressionId,
					exprRef.Arguments
						.Select(a => CreateValueSource<object>(resolvedValues, a, parameters))
						.ToArray()),
				VecValue vec when typeof(T) == typeof(Vector3) => new ConstantSource<T>(
					(T)(object)vec.ToVector3(resolvedValues)),
				VecValue vec when typeof(T) == typeof(Vector2) => new ConstantSource<T>(
					(T)(object)vec.ToVector2(resolvedValues)),
				VecValue vec => new ConstantSource<T>((T)(object)vec.ToVector3(resolvedValues)),
				ColourValue col when typeof(T) == typeof(Color) => new ConstantSource<T>(
					(T)(object)col.ToColor(resolvedValues)),
				Vector3Value v3 when typeof(T) == typeof(Vector3) => new ConstantSource<T>((T)(object)v3.Value),
				Vector3Value v3 when typeof(T) == typeof(Vector2) => new ConstantSource<T>(
					(T)(object)new Vector2(v3.Value.x, v3.Value.y)),
				Vector2Value v2 when typeof(T) == typeof(Vector2) => new ConstantSource<T>((T)(object)v2.Value),
				ColorValue cv when typeof(T) == typeof(Color) => new ConstantSource<T>((T)(object)cv.Value),
				NoValue or null when fallback is not null => new ConstantSource<T>(fallback),
				NoValue or null => None<T>.Instance,
				_ => new ConstantSource<T>(CoerceConstant<T>(raw))
			};

		private static T CoerceConstant<T>(AssemblerValue value)
		{
			if (RefDtoExtensions.TryUnwrap<T>(value, out var unwrapped))
			{
				return unwrapped;
			}

			if (typeof(T) == typeof(object))
			{
				return (T)Unwrap(value);
			}

			throw new ParsingException(
				$"Cannot convert value '{value}' of type '{value.GetType()}' to a {typeof(T)}");
		}

		private static object Unwrap(AssemblerValue value) =>
			value switch
			{
				IntValue i => i.Value,
				FloatValue f => f.Value,
				BoolValue b => b.Value,
				StringValue s => s.Value,
				Vector2Value v => v.Value,
				Vector3Value v => v.Value,
				ColorValue c => c.Value,
				_ => throw new ParsingException($"Cannot unwrap {value.GetType().Name} to object")
			};

		private static AssemblerValue Convert(IReadOnlyList<ValueInfo> resolvedValues, object? obj) =>
			obj switch
			{
				VecDto vecDto => new Vector3Value(vecDto.ToVector3(resolvedValues)),
				ColourDto colourDto => new ColorValue(colourDto.ToColor(resolvedValues)),
				RefDto refDto => ResolveRef(refDto, resolvedValues),
				int i => new IntValue(i),
				float f => new FloatValue(f),
				double d => new FloatValue((float)d),
				bool b => new BoolValue(b),
				string s => new StringValue(s),
				not null => throw new ParsingException($"Cannot convert value of type {obj.GetType()} to a value"),
				_ => throw new ParsingException("Cannot convert null to a value")
			};

		private static AssemblerValue ResolveRef(RefDto refDto, IReadOnlyList<ValueInfo> resolvedValues)
		{
			foreach (var v in resolvedValues)
			{
				if (v.Id == refDto.Id)
				{
					return v.Value;
				}
			}

			throw new ParsingException($"Cannot resolve reference '{refDto.Id}'");
		}

		private static Dictionary<string, AssemblerValue> ConvertProps(IReadOnlyDictionary<string, object>? raw)
		{
			if (raw is null)
			{
				return new Dictionary<string, AssemblerValue>();
			}

			var result = new Dictionary<string, AssemblerValue>(raw.Count);

			foreach (var kvp in raw)
			{
				var converted = ToAssemblerValue(kvp.Value);

				if (converted is not NoValue)
				{
					result[kvp.Key] = converted;
				}
			}

			return result;
		}

		public static AssemblerValue ToAssemblerValue(object? raw) =>
			raw switch
			{
				null => NoValue.Instance,
				AssemblerValue av => av,
				int i => new IntValue(i),
				float f => new FloatValue(f),
				double d => new FloatValue((float)d),
				bool b => new BoolValue(b),
				string s => new StringValue(s),
				VarRefDto v => new VarRef(v.Id ?? string.Empty),
				AssetRefDto v => new AssetRef(v.Id ?? string.Empty),
				OutputRefDto v => new OutputRef(v.Id ?? string.Empty),
				ParamRefDto v => new ParamRef(v.Id ?? string.Empty),
				ExprRefDto v => new ExprRef(v.ExpressionId ?? string.Empty,
					v.Arguments.EmptyIfNull().Select(ToAssemblerValue).ToArray()),
				VecDto v => new VecValue(ToAssemblerValue(v.X), ToAssemblerValue(v.Y), ToAssemblerValue(v.Z)),
				ColourDto v => new ColourValue(ToAssemblerValue(v.R),
					ToAssemblerValue(v.G),
					ToAssemblerValue(v.B),
					ToAssemblerValue(v.A),
					v.Raw is not null ? new StringValue(v.Raw) : NoValue.Instance),
				IDictionary<string, object> dict => new DictValue(ToAssemblerDict(dict)),
				IEnumerable<object> list => new ListValue(ToAssemblerList(list)),
				_ => throw new ParsingException(
					$"Cannot convert raw value '{raw}' (type {raw.GetType()}) to an AssemblerValue")
			};

		private static IReadOnlyDictionary<string, AssemblerValue> ToAssemblerDict(IDictionary<string, object> dict)
		{
			var result = new Dictionary<string, AssemblerValue>(dict.Count);

			foreach (var kvp in dict)
			{
				var converted = ToAssemblerValue(kvp.Value);

				if (converted is not NoValue)
				{
					result[kvp.Key] = converted;
				}
			}

			return result;
		}

		private static IReadOnlyList<AssemblerValue> ToAssemblerList(IEnumerable<object> list) =>
			list.Select(ToAssemblerValue).Where(converted => converted is not NoValue).ToList();
	}
}