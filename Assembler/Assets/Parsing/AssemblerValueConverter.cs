using System.Collections.Generic;
using System.Linq;
using Assembler.Core;
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

		// Unified object -> AssemblerValue converter. A single switch over (raw, conversion) reproduces all
		// three historical stages: the raw value selects the shape, and the context pattern selects the
		// stage (ResolvedValues set ⇒ resolved, AllowRuntimeTypes ⇒ runtime, neither ⇒ IR).
		public static AssemblerValue ToAssemblerValue(object? raw, ValueConversion conversion) =>
			(raw, conversion) switch
			{
				(null, { RejectNull: true }) => throw new ParsingException(
					$"Cannot convert null to a value{(conversion.Name is null ? string.Empty : $" (for '{conversion.Name}')")}"),
				(null, _) => NoValue.Instance,
				// Scalars convert identically at every stage.
				(int i, _) => new IntValue(i),
				(float f, _) => new FloatValue(f),
				(double d, _) => new FloatValue((float)d),
				(bool b, _) => new BoolValue(b),
				(string s, _) => new StringValue(s),
				// An already-converted value passes through at the IR and runtime stages; the resolved stage
				// (which only sees raw DTOs) rejects it by falling through to the failure below.
				(AssemblerValue av, { ResolvedValues: null }) => av,
				// Resolved stage: any *RefDto (var/asset/entity-position/output/param) dereferences to its
				// resolved value. The IR mapping below handles the unresolved (IR) stage.
				(RefDto refDto, { ResolvedValues: { } resolved }) => ResolveRef(refDto, resolved),
				// Resolved stage: VecDto/ColourDto evaluate to a concrete Vector3Value/ColorValue.
				(VecDto vec, { ResolvedValues: { } resolved }) => new Vector3Value(vec.ToVector3(resolved)),
				(ColourDto col, { ResolvedValues: { } resolved }) => new ColorValue(col.ToColor(resolved)),
				// Runtime stage: concrete engine types produced by evaluated expressions.
				(Vector3 v, { AllowRuntimeTypes: true }) => new Vector3Value(v),
				// A runtime expression can still evaluate to a Vector2 (the compiler resolves any loaded type,
				// e.g. Random.insideUnitCircle); widen it to a Vector3 (z = 0), since Vector2 is no longer a
				// domain value type.
				(Vector2 v, { AllowRuntimeTypes: true }) => new Vector3Value(v),
				(Color c, { AllowRuntimeTypes: true }) => new ColorValue(c),
				// IR stage: DTOs become unevaluated AssemblerValue IR. These inputs only occur in the IR
				// stage; the resolved stage handles RefDto above (and rejects everything else) and the runtime
				// stage never receives DTOs.
				(VarRefDto v, { ResolvedValues: null }) => new VarRef(v.Id ?? string.Empty),
				(AssetRefDto v, { ResolvedValues: null }) => new AssetRef(v.Id ?? string.Empty),
				(EntityRefDto v, { ResolvedValues: null }) => new EntityPropertyRef(v.Id ?? string.Empty, ParseEntityProperty(v.Property)),
				(RigidbodyRefDto v, { ResolvedValues: null }) => new RigidbodyPropertyRef(v.Id ?? string.Empty, ParseRigidbodyProperty(v.Property)),
				(ClockRefDto v, { ResolvedValues: null }) => new ClockRef(v.Property ?? string.Empty),
				(EntityQueryRefDto v, { ResolvedValues: null }) => new QueryRef(
					v.Kind ?? string.Empty,
					v.EntityTag ?? string.Empty,
					ToAssemblerValue(v.Origin),
					ToAssemblerValue(v.MaxRange)),
				(OutputRefDto v, { ResolvedValues: null }) => new OutputRef(v.Id ?? string.Empty),
				(ParamRefDto v, { ResolvedValues: null }) => new ParamRef(v.Id ?? string.Empty),
				// A nested `!gameover` (e.g. inside a state machine OnEnter/OnExit list) deserialises to a
				// GameOverListenerDto via the global tag mapping; carry it through as a marker so the
				// nested-listener parser can rebuild a GameOverListenerInfo.
				(GameOverListenerDto, { ResolvedValues: null }) => new GameOverMarker(),
				(ExprRefDto v, { ResolvedValues: null }) => new ExprRef(v.Do ?? string.Empty,
					v.With.EmptyIfNull().Select(ToAssemblerValue).ToArray(),
					v.ReturnType,
					v.ArgumentTypes,
					v.RegisterTypes,
					v.RegisterTypeStatics),
				(TextRefDto v, { ResolvedValues: null }) => new TextRef(v.Key ?? string.Empty,
					v.Arguments.EmptyIfNull().Select(ToAssemblerValue).ToArray()),
				(VecDto v, { ResolvedValues: null }) =>
					new VecValue(ToAssemblerValue(v.X), ToAssemblerValue(v.Y), ToAssemblerValue(v.Z)),
				(ColourDto v, { ResolvedValues: null }) => new ColourValue(ToAssemblerValue(v.R),
					ToAssemblerValue(v.G),
					ToAssemblerValue(v.B),
					ToAssemblerValue(v.A),
					v.Raw is null ? NoValue.Instance : new StringValue(v.Raw)),
				// Typed lists are accepted at the IR and resolved stages (and rejected at runtime). Each
				// element recurses with the same context, so VecDto/ColourDto elements evaluate at the
				// resolved stage and stay unevaluated at the IR stage, exactly as a scalar would.
				(List<VecDto> vecList, { AllowRuntimeTypes: false }) => new TypedListValue(typeof(Vector3),
					vecList.ConvertAll<AssemblerValue>(v => ToAssemblerValue(v, conversion))),
				(List<ColourDto> colourList, { AllowRuntimeTypes: false }) => new TypedListValue(typeof(Color),
					colourList.ConvertAll<AssemblerValue>(c => ToAssemblerValue(c, conversion))),
				(List<int> intList, { AllowRuntimeTypes: false }) =>
					new TypedListValue(typeof(int), intList.ConvertAll<AssemblerValue>(i => new IntValue(i))),
				(List<float> floatList, { AllowRuntimeTypes: false }) => new TypedListValue(typeof(float),
					floatList.ConvertAll<AssemblerValue>(f => new FloatValue(f))),
				(List<bool> boolList, { AllowRuntimeTypes: false }) => new TypedListValue(typeof(bool),
					boolList.ConvertAll<AssemblerValue>(b => new BoolValue(b))),
				(List<string> stringList, { AllowRuntimeTypes: false }) => new TypedListValue(typeof(string),
					stringList.ConvertAll<AssemblerValue>(s => new StringValue(s))),
				// A `!record` literal becomes a RecordValue at every non-runtime stage (its field values
				// recurse with the same context, so nested refs deref at the resolved stage); the transform
				// later completes it against its schema. A `!record []` list becomes a Record-typed list.
				(RecordLiteralDto rec, { AllowRuntimeTypes: false }) =>
					new RecordValue(rec.Type ?? string.Empty, ToAssemblerFields(rec.Fields, conversion)),
				(List<RecordLiteralDto> recList, { AllowRuntimeTypes: false }) => new TypedListValue(typeof(Record),
					recList.ConvertAll<AssemblerValue>(r => ToAssemblerValue(r, conversion))),
				// IR stage only: untyped collections become Dict/List IR (used by nested listener maps). The
				// resolved and runtime stages reject them — an untyped/mixed sequence is a conversion error.
				(IDictionary<string, object> dict, { ResolvedValues: null, AllowRuntimeTypes: false }) =>
					new DictValue(ToAssemblerDict(dict)),
				(IEnumerable<object> list, { ResolvedValues: null, AllowRuntimeTypes: false }) =>
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

		private static EntityProperty ParseEntityProperty(string? property) =>
			(property ?? string.Empty).Trim().ToLowerInvariant() switch
			{
				"position" => EntityProperty.Position,
				"rotation" => EntityProperty.Rotation,
				"scale" => EntityProperty.Scale,
				_ => throw new ParsingException(
					$"Unknown !entity property '{property}'. Expected one of: Position, Rotation, Scale")
			};

		private static RigidbodyProperty ParseRigidbodyProperty(string? property) =>
			(property ?? string.Empty).Trim().ToLowerInvariant() switch
			{
				"velocity" => RigidbodyProperty.Velocity,
				"angularvelocity" => RigidbodyProperty.AngularVelocity,
				"position" => RigidbodyProperty.Position,
				"rotation" => RigidbodyProperty.Rotation,
				_ => throw new ParsingException(
					$"Unknown !rigidbody property '{property}'. Expected one of: Velocity, AngularVelocity, Position, Rotation")
			};

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

		// Converts a record literal's explicitly-set fields into AssemblerValue IR, recursing with the same
		// conversion context so a nested ref/vec/etc. is handled exactly as a top-level value would be.
		private static IReadOnlyDictionary<string, AssemblerValue> ToAssemblerFields(
			IReadOnlyDictionary<string, object> fields, ValueConversion conversion)
		{
			var result = new Dictionary<string, AssemblerValue>(fields.Count);

			foreach (var kvp in fields)
			{
				var converted = ToAssemblerValue(kvp.Value, conversion);

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
