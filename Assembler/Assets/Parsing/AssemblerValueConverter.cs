using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Deserialisation.Dtos;
using Assembler.Extensions;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Parsing
{
	// Selects how the unified object -> AssemblerValue converter behaves at each pipeline stage.
	// The three historical converters (IR-stage ToAssemblerValue, resolved-stage Convert,
	// runtime-stage AdaptRuntimeParameter) collapse into one method gated by this context.
	internal readonly struct ValueConversion
	{
		// Non-null ⇒ resolved stage: VecDto/ColourDto are materialised to Vector3Value/ColorValue and
		// any *RefDto is dereferenced against these values. Null ⇒ IR stage (unevaluated VecValue/etc.).
		public IReadOnlyList<ValueInfo>? ResolvedValues { get; init; }

		// True ⇒ accept concrete runtime types (Vector3/Vector2/Color) produced by evaluated expressions.
		public bool AllowRuntimeTypes { get; init; }

		// True ⇒ a null input throws rather than producing NoValue.
		public bool RejectNull { get; init; }

		// Field/parameter name used in failure messages.
		public string? Name { get; init; }
	}

	// Converts raw deserialised values (DTOs, primitives, collections, and concrete runtime types) into
	// the AssemblerValue IR. One switch reproduces all three historical pipeline stages, branching on a
	// <see cref="ValueConversion"/> context.
	internal static class AssemblerValueConverter
	{
		// IR-stage entry point: DTOs become unevaluated AssemblerValue IR (VecValue/ColourValue, *Ref,
		// typed lists, dict/list), null becomes NoValue, concrete C# types are rejected.
		public static AssemblerValue ToAssemblerValue(object? raw) => ToAssemblerValue(raw, default);

		// Resolved-stage entry point: materialises VecDto/ColourDto and dereferences refs against the
		// resolved values, and treats a null input as an error.
		public static AssemblerValue Convert(IReadOnlyList<ValueInfo> resolvedValues, object? obj, string? name = null) =>
			ToAssemblerValue(obj, new ValueConversion { ResolvedValues = resolvedValues, RejectNull = true, Name = name });

		// Unified object -> AssemblerValue converter. A single switch reproduces all three historical
		// stages, branching on the <see cref="ValueConversion"/> context.
		public static AssemblerValue ToAssemblerValue(object? raw, ValueConversion conversion) =>
			raw switch
			{
				null when conversion.RejectNull => throw new ParsingException(
					$"Cannot convert null to a value{(conversion.Name is null ? string.Empty : $" (for '{conversion.Name}')")}"),
				null => NoValue.Instance,
				// Scalars convert identically at every stage.
				int i => new IntValue(i),
				float f => new FloatValue(f),
				double d => new FloatValue((float)d),
				bool b => new BoolValue(b),
				string s => new StringValue(s),
				// An already-converted value passes through at the IR and runtime stages; the resolved stage
				// (which only sees raw DTOs) rejects it by falling through to the failure below.
				AssemblerValue av when conversion.ResolvedValues is null => av,
				// Resolved stage: any *RefDto (var/asset/entity-position/output/param) dereferences to its
				// resolved value. The IR mapping below handles the unresolved (IR) stage.
				RefDto refDto when conversion.ResolvedValues is { } resolved => ResolveRef(refDto, resolved),
				// Resolved stage: VecDto/ColourDto evaluate to a concrete Vector3Value/ColorValue.
				VecDto vec when conversion.ResolvedValues is { } resolved => new Vector3Value(vec.ToVector3(resolved)),
				ColourDto col when conversion.ResolvedValues is { } resolved => new ColorValue(col.ToColor(resolved)),
				// Runtime stage: concrete engine types produced by evaluated expressions.
				Vector3 v when conversion.AllowRuntimeTypes => new Vector3Value(v),
				// A runtime expression can still evaluate to a Vector2 (the compiler resolves any loaded type,
				// e.g. Random.insideUnitCircle); widen it to a Vector3 (z = 0), since Vector2 is no longer a
				// domain value type.
				Vector2 v when conversion.AllowRuntimeTypes => new Vector3Value(v),
				Color c when conversion.AllowRuntimeTypes => new ColorValue(c),
				// IR stage: DTOs become unevaluated AssemblerValue IR. These inputs only occur in the IR
				// stage; the resolved stage handles RefDto above (and rejects everything else) and the runtime
				// stage never receives DTOs.
				VarRefDto v when conversion.ResolvedValues is null => new VarRef(v.Id ?? string.Empty),
				AssetRefDto v when conversion.ResolvedValues is null => new AssetRef(v.Id ?? string.Empty),
				EntityPositionRefDto v when conversion.ResolvedValues is null => new EntityPositionRef(v.Id ?? string.Empty),
				ClockRefDto v when conversion.ResolvedValues is null => new ClockRef(v.Property ?? string.Empty),
				OutputRefDto v when conversion.ResolvedValues is null => new OutputRef(v.Id ?? string.Empty),
				ParamRefDto v when conversion.ResolvedValues is null => new ParamRef(v.Id ?? string.Empty),
				// A nested `!gameover` (e.g. inside a state machine OnEnter/OnExit list) deserialises to a
				// GameOverListenerDto via the global tag mapping; carry it through as a marker so the
				// nested-listener parser can rebuild a GameOverListenerInfo.
				GameOverListenerDto when conversion.ResolvedValues is null => new GameOverMarker(),
				ExprRefDto v when conversion.ResolvedValues is null => new ExprRef(v.Do ?? string.Empty,
					v.With.EmptyIfNull().Select(ToAssemblerValue).ToArray(),
					v.ReturnType,
					v.ArgumentTypes,
					v.RegisterTypes,
					v.RegisterTypeStatics),
				TextRefDto v when conversion.ResolvedValues is null => new TextRef(v.Key ?? string.Empty,
					v.Arguments.EmptyIfNull().Select(ToAssemblerValue).ToArray()),
				VecDto v when conversion.ResolvedValues is null =>
					new VecValue(ToAssemblerValue(v.X), ToAssemblerValue(v.Y), ToAssemblerValue(v.Z)),
				ColourDto v when conversion.ResolvedValues is null => new ColourValue(ToAssemblerValue(v.R),
					ToAssemblerValue(v.G),
					ToAssemblerValue(v.B),
					ToAssemblerValue(v.A),
					v.Raw is null ? NoValue.Instance : new StringValue(v.Raw)),
				// Typed lists are accepted at the IR and resolved stages (and rejected at runtime). Each
				// element recurses with the same context, so VecDto/ColourDto elements evaluate at the
				// resolved stage and stay unevaluated at the IR stage, exactly as a scalar would.
				List<VecDto> vecList when !conversion.AllowRuntimeTypes => new TypedListValue(typeof(Vector3),
					vecList.ConvertAll<AssemblerValue>(v => ToAssemblerValue(v, conversion))),
				List<ColourDto> colourList when !conversion.AllowRuntimeTypes => new TypedListValue(typeof(Color),
					colourList.ConvertAll<AssemblerValue>(c => ToAssemblerValue(c, conversion))),
				List<int> intList when !conversion.AllowRuntimeTypes =>
					new TypedListValue(typeof(int), intList.ConvertAll<AssemblerValue>(i => new IntValue(i))),
				List<float> floatList when !conversion.AllowRuntimeTypes => new TypedListValue(typeof(float),
					floatList.ConvertAll<AssemblerValue>(f => new FloatValue(f))),
				List<bool> boolList when !conversion.AllowRuntimeTypes => new TypedListValue(typeof(bool),
					boolList.ConvertAll<AssemblerValue>(b => new BoolValue(b))),
				List<string> stringList when !conversion.AllowRuntimeTypes => new TypedListValue(typeof(string),
					stringList.ConvertAll<AssemblerValue>(s => new StringValue(s))),
				// IR stage only: untyped collections become Dict/List IR (used by nested listener maps). The
				// resolved and runtime stages reject them — an untyped/mixed sequence is a conversion error.
				IDictionary<string, object> dict when conversion.ResolvedValues is null && !conversion.AllowRuntimeTypes =>
					new DictValue(ToAssemblerDict(dict)),
				IEnumerable<object> list when conversion.ResolvedValues is null && !conversion.AllowRuntimeTypes =>
					new ListValue(ToAssemblerList(list)),
				_ => throw new ParsingException(DescribeConvertFailure(raw, conversion))
			};

		// Builds the failure message for a value the converter can't handle. The wording matches the
		// originating stage so existing diagnostics are preserved: the runtime stage names the parameter,
		// the resolved stage names the field (and lists element types for an untyped collection — e.g. a
		// mixed YAML sequence that deserialises to List<object>), and the IR stage reports the raw value.
		private static string DescribeConvertFailure(object obj, ValueConversion conversion)
		{
			if (conversion.AllowRuntimeTypes)
			{
				return $"Cannot adapt runtime parameter '{conversion.Name}' (type {obj.GetType()}) for template instantiation";
			}

			if (conversion.ResolvedValues != null)
			{
				var forField = conversion.Name is null ? string.Empty : $" for '{conversion.Name}'";

				if (obj is System.Collections.IEnumerable enumerable and not string)
				{
					var elementTypes = enumerable.Cast<object?>()
						.Select(item => item?.GetType().Name ?? "null")
						.Distinct()
						.ToArray();

					return $"Cannot convert value of type {obj.GetType()} to a value{forField} " +
						   $"(element types: {string.Join(", ", elementTypes)})";
				}

				return $"Cannot convert value of type {obj.GetType()} to a value{forField}";
			}

			return $"Cannot convert raw value '{obj}' (type {obj.GetType()}) to an AssemblerValue";
		}

		private static AssemblerValue ResolveRef(RefDto refDto, IReadOnlyList<ValueInfo> resolvedValues) =>
			resolvedValues.ResolveValue(refDto.Id);

		public static Dictionary<string, AssemblerValue> ConvertProps(IReadOnlyDictionary<string, object>? raw)
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
