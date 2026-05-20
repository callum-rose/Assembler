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

			var allValues = (gameDto.Constants ?? new Dictionary<string, object>())
				.Concat(gameDto.Variables ?? new Dictionary<string, object>());

			foreach (var kvp in allValues)
			{
				values.Add(new ValueInfo(kvp.Key, Convert(values, kvp.Value)));
			}

			var expressions = gameDto.Expressions?.Select(kvp => new ExpressionInfo(kvp.Key,
				(kvp.Value.ArgumentTypes ?? Array.Empty<string>())
				.Zip(kvp.Value.ArgumentNames ?? Array.Empty<string>(), (type, name) => (type, name)).ToArray(),
				kvp.Value.ReturnType ?? string.Empty,
				kvp.Value.RegisterTypes ?? Array.Empty<string>(),
				kvp.Value.RegisterTypeStatics ?? Array.Empty<string>(),
				kvp.Value.Expression ?? string.Empty)).ToArray();

			var templates = gameDto.Templates?
				.Select(kvp => new ConcreteEntityInfo(
					kvp.Key,
					kvp.Value.Tags ?? new List<string>(),
					CreateValueSource<Vector3>(values, ToAssemblerValue(kvp.Value.Position)),
					CreateValueSource<Vector3>(values, ToAssemblerValue(kvp.Value.Rotation)),
					(kvp.Value.Behaviours ?? new Dictionary<string, BehaviourDto>())
						.Select(b => CreateBehaviour(values, b.Key, b.Value, new Dictionary<string, AssemblerValue>()))
						.ToArray(),
					CreateEntityVariables(kvp.Value.Variables)))
				.ToArray() ?? Array.Empty<ConcreteEntityInfo>();

			var entities = (gameDto.Entities ?? new Dictionary<string, EntityDto>())
				.Select(kvp => CreateEntityInfo(kvp.Key, kvp.Value)).ToArray();

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

			ConcreteEntityInfo CreateEntityInfo(string entityId, EntityDto entityDto)
			{
				EntityInfo template;
				Dictionary<string, AssemblerValue> parameters;

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

				var ownBehaviours = (entityDto.Behaviours ?? new Dictionary<string, BehaviourDto>())
					.Select(b => CreateBehaviour(values, b.Key, b.Value, parameters));

				return TemplateInstantiator.Instantiate(template,
					entityId,
					values,
					CreateValueSource<Vector3>(values, ToAssemblerValue(entityDto.Position), parameters: parameters),
					CreateValueSource<Vector3>(values, ToAssemblerValue(entityDto.Rotation), parameters: parameters),
					parameters,
					entityDto.Tags,
					ownBehaviours,
					CreateEntityVariables(entityDto.Variables));
			}
		}

		internal static IReadOnlyList<ValueInfo> CreateEntityVariables(IReadOnlyDictionary<string, object>? variables)
		{
			if (variables == null || variables.Count == 0)
			{
				return Array.Empty<ValueInfo>();
			}

			var result = new ValueInfo[variables.Count];
			var i = 0;

			foreach (var kvp in variables)
			{
				result[i++] = new ValueInfo(kvp.Key, ToAssemblerValue(kvp.Value));
			}

			return result;
		}

		internal static AssemblerValue SubstituteAssemblerValue(AssemblerValue value,
			IReadOnlyDictionary<string, AssemblerValue> parameters)
		{
			return value switch
			{
				ParamRef paramRef => parameters.TryGetValue(paramRef.Id, out var resolved)
					? resolved
					: throw new ParsingException(
						$"Parameter '{paramRef.Id}' not supplied during template instantiation"),
				VecValue vec => new VecValue(
					SubstituteAssemblerValue(vec.X, parameters),
					SubstituteAssemblerValue(vec.Y, parameters),
					SubstituteAssemblerValue(vec.Z, parameters)),
				ColourValue col => new ColourValue(
					SubstituteAssemblerValue(col.R, parameters),
					SubstituteAssemblerValue(col.G, parameters),
					SubstituteAssemblerValue(col.B, parameters),
					SubstituteAssemblerValue(col.A, parameters),
					col.Raw),
				ExprRef exprRef => new ExprRef(exprRef.ExpressionId,
					exprRef.Arguments.Select(a => SubstituteAssemblerValue(a, parameters)).ToArray()),
				_ => value
			};
		}

		private static BehaviourInfo CreateBehaviour(IReadOnlyList<ValueInfo> resolvedValues,
			string id,
			BehaviourDto behaviourDto,
			IReadOnlyDictionary<string, AssemblerValue> parameters)
		{
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
					var outputs = l.Outputs ?? new Dictionary<string, string>();

					if (l.EntityTag != null || l.BehaviourTag != null)
					{
						var entityTag = l.EntityTag != null
							? CreateValueSource<string>(variables, ToAssemblerValue(l.EntityTag), parameters)
							: null;
						var behaviourTag = l.BehaviourTag != null
							? CreateValueSource<string>(variables, ToAssemblerValue(l.BehaviourTag), parameters)
							: null;

						return (ListenerInfo)new TaggedListenerInfo(entityTag, behaviourTag, l.BehaviourId)
						{
							OutputMapping = outputs
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

					return new DirectListenerInfo(behaviourDescriptor)
					{
						OutputMapping = outputs
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
				TypedListValue typed when IsAssignableList(typeof(T), typed.ElementType) =>
					new ConstantSource<T>((T)BuildTypedList(typed)),
				ListValue list when TryGetListElementType(typeof(T), out var elementType) =>
					new ConstantSource<T>((T)BuildListFromUntyped(list, elementType!, resolvedValues, parameters)),
				NoValue or null when fallback is not null => new ConstantSource<T>(fallback),
				NoValue or null => None<T>.Instance,
				_ => new ConstantSource<T>(CoerceConstant<T>(raw))
			};

		private static bool IsAssignableList(Type t, Type elementType)
		{
			if (!t.IsGenericType)
			{
				return false;
			}

			var genericDef = t.GetGenericTypeDefinition();

			if (genericDef != typeof(IList<>) &&
			    genericDef != typeof(IReadOnlyList<>) &&
			    genericDef != typeof(IEnumerable<>) &&
			    genericDef != typeof(List<>))
			{
				return false;
			}

			return t.GetGenericArguments()[0] == elementType;
		}

		private static bool TryGetListElementType(Type t, out Type? elementType)
		{
			elementType = null;

			if (!t.IsGenericType)
			{
				return false;
			}

			var genericDef = t.GetGenericTypeDefinition();

			if (genericDef != typeof(IList<>) &&
			    genericDef != typeof(IReadOnlyList<>) &&
			    genericDef != typeof(IEnumerable<>) &&
			    genericDef != typeof(List<>))
			{
				return false;
			}

			elementType = t.GetGenericArguments()[0];
			return true;
		}

		private static object BuildTypedList(TypedListValue typed)
		{
			var listType = typeof(List<>).MakeGenericType(typed.ElementType);
			var list = (System.Collections.IList)Activator.CreateInstance(listType, typed.Items.Count);

			foreach (var item in typed.Items)
			{
				list.Add(UnwrapPrimitive(item, typed.ElementType));
			}

			return list;
		}

		private static object BuildListFromUntyped(ListValue list,
			Type elementType,
			IReadOnlyList<ValueInfo> resolvedValues,
			IReadOnlyDictionary<string, AssemblerValue> parameters)
		{
			var listType = typeof(List<>).MakeGenericType(elementType);
			var result = (System.Collections.IList)Activator.CreateInstance(listType, list.Value.Count);

			foreach (var item in list.Value)
			{
				result.Add(UnwrapPrimitive(item, elementType));
			}

			return result;
		}

		private static object UnwrapPrimitive(AssemblerValue value, Type expectedType)
		{
			return value switch
			{
				IntValue i when expectedType == typeof(int) => i.Value,
				IntValue i when expectedType == typeof(float) => (float)i.Value,
				FloatValue f when expectedType == typeof(float) => f.Value,
				BoolValue b when expectedType == typeof(bool) => b.Value,
				StringValue s when expectedType == typeof(string) => s.Value,
				Vector2Value v when expectedType == typeof(Vector2) => v.Value,
				Vector3Value v when expectedType == typeof(Vector3) => v.Value,
				ColorValue c when expectedType == typeof(Color) => c.Value,
				_ => throw new ParsingException(
					$"List element {value.GetType().Name} cannot be coerced to {expectedType.Name}")
			};
		}

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
				List<VecDto> vecList => new TypedListValue(typeof(Vector3),
					vecList.ConvertAll(v => (AssemblerValue)new Vector3Value(v.ToVector3(resolvedValues)))),
				List<ColourDto> colourList => new TypedListValue(typeof(Color),
					colourList.ConvertAll(c => (AssemblerValue)new ColorValue(c.ToColor(resolvedValues)))),
				List<int> intList => new TypedListValue(typeof(int),
					intList.ConvertAll(i => (AssemblerValue)new IntValue(i))),
				List<float> floatList => new TypedListValue(typeof(float),
					floatList.ConvertAll(f => (AssemblerValue)new FloatValue(f))),
				List<bool> boolList => new TypedListValue(typeof(bool),
					boolList.ConvertAll(b => (AssemblerValue)new BoolValue(b))),
				List<string> stringList => new TypedListValue(typeof(string),
					stringList.ConvertAll(s => (AssemblerValue)new StringValue(s))),
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
				List<VecDto> vecList => new TypedListValue(typeof(Vector3),
					vecList.ConvertAll(v => ToAssemblerValue(v))),
				List<ColourDto> colourList => new TypedListValue(typeof(Color),
					colourList.ConvertAll(c => ToAssemblerValue(c))),
				List<int> intList => new TypedListValue(typeof(int),
					intList.ConvertAll(i => (AssemblerValue)new IntValue(i))),
				List<float> floatList => new TypedListValue(typeof(float),
					floatList.ConvertAll(f => (AssemblerValue)new FloatValue(f))),
				List<bool> boolList => new TypedListValue(typeof(bool),
					boolList.ConvertAll(b => (AssemblerValue)new BoolValue(b))),
				List<string> stringList => new TypedListValue(typeof(string),
					stringList.ConvertAll(s => (AssemblerValue)new StringValue(s))),
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