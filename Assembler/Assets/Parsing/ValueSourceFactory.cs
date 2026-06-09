using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Parsing
{
	// Wraps a parsed AssemblerValue into a strongly-typed ValueSource&lt;T&gt; (constants, variable/expression/
	// asset references, typed lists, …), and provides the list/argument coercion helpers behaviour Info
	// factories use when reading their properties.
	public static class ValueSourceFactory
	{
		// `CreateValueSourceForArg` is invoked via reflection (one MakeGenericMethod per arg type
		// the parser actually encounters). This is a method handle constant, not mutable state —
		// the per-call cache of closed-generic MethodInfos lives on TransformContext.
		private readonly static MethodInfo CreateValueSourceForArgOpenGeneric =
			typeof(ValueSourceFactory).GetMethod(nameof(CreateValueSourceForArg),
				BindingFlags.NonPublic | BindingFlags.Static)!;

		private static ValueSource<T> CreateValueSourceForArg<T>(TransformContext ctx, AssemblerValue raw) =>
			raw is NoValue ? None<T>.Instance : CreateValueSource<T>(ctx, raw);

		internal static IReadOnlyList<string> ConvertStringList(AssemblerValue? value) =>
			value is ListValue list
				? list.Value
					.Select(item => item is StringValue sv ? sv.Value : item?.ToString() ?? string.Empty)
					.ToArray()
				: Array.Empty<string>();

		internal static IReadOnlyList<IValueSourceArg> ConvertArgumentList(TransformContext ctx,
			AssemblerValue? value) =>
			value is ListValue list
				? list.Value.Select(item => (IValueSourceArg)
					CreateValueSource<object>(ctx, item)).ToArray()
				: Array.Empty<IValueSourceArg>();

		private static IReadOnlyList<IValueSourceArg> BuildTextArguments(TransformContext ctx, TextRef textRef)
		{
			if (textRef.Arguments.Count == 0)
			{
				return Array.Empty<IValueSourceArg>();
			}

			var args = new IValueSourceArg[textRef.Arguments.Count];

			for (int i = 0; i < textRef.Arguments.Count; i++)
			{
				// !text placeholders have no declared types — each argument is boxed to object and
				// stringified by string.Format at runtime, so every argument resolves as object.
				args[i] = CreateValueSource<object>(ctx, textRef.Arguments[i]);
			}

			return args;
		}

		// Wraps one `With` operand as a strongly-typed ValueSource<argType>, via the cached
		// closed-generic CreateValueSourceForArg factory.
		internal static IValueSourceArg BuildArg(TransformContext ctx, Type argType, AssemblerValue raw)
		{
			if (!ctx.ExprArgFactoryCache.TryGetValue(argType, out var typed))
			{
				typed = CreateValueSourceForArgOpenGeneric.MakeGenericMethod(argType);
				ctx.ExprArgFactoryCache[argType] = typed;
			}

			return (IValueSourceArg)typed.Invoke(null, new object?[] { ctx, raw })!;
		}

		private static ClockProperty ParseClockProperty(string property) =>
			property.Trim().ToLowerInvariant() switch
			{
				"deltatime" => ClockProperty.DeltaTime,
				"time" => ClockProperty.Time,
				"framecount" => ClockProperty.FrameCount,
				"unscaleddeltatime" => ClockProperty.UnscaledDeltaTime,
				_ => throw new ParsingException(
					$"Unknown !clock property '{property}'. Expected one of: deltaTime, time, frameCount, unscaledDeltaTime")
			};

		/// <summary>
		/// Like <see cref="CreateValueSource{T}"/> but with no implicit fallback: an absent value (null or
		/// <see cref="NoValue"/>) resolves to <see cref="None{T}"/> — i.e. a <c>NullValueProvider</c> at
		/// runtime — for value types as well as reference types. Use this for optional properties whose
		/// default is supplied at the point of use via <c>ValueOr</c>; the base <see cref="CreateValueSource{T}"/>
		/// would instead produce a <c>ConstantSource(default(T))</c> for value types (e.g. 0 / (0,0,0)).
		/// </summary>
		public static ValueSource<T> CreateOptionalValueSource<T>(TransformContext ctx, AssemblerValue? raw) =>
			raw is null or NoValue ? None<T>.Instance : CreateValueSource<T>(ctx, raw);

		public static ValueSource<T> CreateValueSource<T>(TransformContext ctx,
			AssemblerValue raw,
			T? fallback = default) =>
			raw switch
			{
				ParamRef paramRef => ctx.Parameters.TryGetValue(paramRef.Id, out var paramValue)
					? CreateValueSource(ctx, paramValue, fallback)
					: new ParameterSource<T>(paramRef.Id),
				AssetRef assetRef => new AssetSource<T>(assetRef.Id),
				EntityPropertyRef entityPropertyRef when typeof(T) == typeof(Vector3) || typeof(T) == typeof(object) =>
					new EntityPropertySource<T>(entityPropertyRef.Id, entityPropertyRef.Property),
				EntityPropertyRef entityPropertyRef => throw new ParsingException(
					$"!entity '{entityPropertyRef.Id}' property '{entityPropertyRef.Property}' resolves to Vector3 but was used where a {typeof(T).Name} was expected"),
				RigidbodyPropertyRef rigidbodyPropertyRef when typeof(T) == typeof(Vector3) || typeof(T) == typeof(object) =>
					new RigidbodyPropertySource<T>(rigidbodyPropertyRef.Id, rigidbodyPropertyRef.Property),
				RigidbodyPropertyRef rigidbodyPropertyRef => throw new ParsingException(
					$"!rigidbody '{rigidbodyPropertyRef.Id}' property '{rigidbodyPropertyRef.Property}' resolves to Vector3 but was used where a {typeof(T).Name} was expected"),
				ClockRef clockRef when typeof(T) == typeof(float) || typeof(T) == typeof(int)
					|| typeof(T) == typeof(double) || typeof(T) == typeof(object) =>
					new ClockValueSource<T>(ParseClockProperty(clockRef.Property)),
				ClockRef clockRef => throw new ParsingException(
					$"!clock '{clockRef.Property}' resolves to a numeric value but was used where a {typeof(T).Name} was expected"),
				OutputRef outputRef => new TriggerOutputSource<T>(outputRef.Id),
				TextRef textRef when typeof(T) == typeof(string) =>
					new LocalisedTextSource<T>(textRef.Key, BuildTextArguments(ctx, textRef)),
				TextRef textRef => throw new ParsingException(
					$"!text '{textRef.Key}' resolves to a string but was used where a {typeof(T).Name} was expected"),
				VarRef varRef => new ValueReferenceSource<T>(varRef.Id),
				ExprRef exprRef => ExpressionSynthesis.CreateExpressionSource<T>(ctx, exprRef),
				VecValue vec when typeof(T) == typeof(Vector3) => new ConstantSource<T>(
					(T)(object)vec.ToVector3(ctx.Values)),
				VecValue vec => new ConstantSource<T>((T)(object)vec.ToVector3(ctx.Values)),
				ColourValue col when typeof(T) == typeof(Color) => new ConstantSource<T>(
					(T)(object)col.ToColor(ctx.Values)),
				Vector3Value v3 when typeof(T) == typeof(Vector3) => new ConstantSource<T>((T)(object)v3.Value),
				ColorValue cv when typeof(T) == typeof(Color) => new ConstantSource<T>((T)(object)cv.Value),
				TypedListValue typed when IsAssignableList(typeof(T), typed.ElementType) =>
					new ConstantSource<T>((T)BuildTypedList(typed)),
				ListValue list when TryGetListElementType(typeof(T), out var elementType) =>
					new ConstantSource<T>((T)BuildListFromUntyped(list, elementType!)),
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

			if (genericDef != typeof(IReadOnlyList<>) &&
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

			if (genericDef != typeof(IReadOnlyList<>) &&
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

		private static object BuildListFromUntyped(ListValue list, Type elementType)
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
				Vector3Value v => v.Value,
				ColorValue c => c.Value,
				_ => throw new ParsingException($"Cannot unwrap {value.GetType().Name} to object")
			};
	}
}
