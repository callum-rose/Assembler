using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Parsing.Info
{

	public interface IValueSourceArg
	{
		IValueSourceArg SubstituteParameters(TransformContext ctx);

		object Resolve(IValueSourceResolver resolver);
	}

	public interface IValueSourceResolver
	{
		object Resolve<T>(ValueSource<T> source);
	}

	public abstract record ValueSource<T> : IValueSourceArg
	{
		public virtual ValueSource<T> SubstituteParameters(TransformContext ctx) => this;

		IValueSourceArg IValueSourceArg.SubstituteParameters(TransformContext ctx) => SubstituteParameters(ctx);

		public object Resolve(IValueSourceResolver resolver) => resolver.Resolve(this);
	}

	public sealed record None<T> : ValueSource<T>
	{
		public readonly static None<T> Instance = new();
	}

	public sealed record ConstantSource<T>(T Value) : ValueSource<T>;

	public sealed record ValueReferenceSource<T>(string VariableId) : ValueSource<T>;

	public sealed record ExpressionSource<T>(
		string ExpressionId,
		IReadOnlyList<IValueSourceArg> Arguments) : ValueSource<T>
	{
		public override ValueSource<T> SubstituteParameters(TransformContext ctx) =>
			new ExpressionSource<T>(ExpressionId,
				Arguments.Select(a => a.SubstituteParameters(ctx)).ToArray());
	}

	public sealed record ParameterSource<T>(string ParameterId) : ValueSource<T>
	{
		public override ValueSource<T> SubstituteParameters(TransformContext ctx) =>
			ctx.Parameters.TryGetValue(ParameterId, out var raw)
				? ValueSourceFactory.CreateValueSource<T>(ctx, raw)
				: throw new ParsingException($"Parameter '{ParameterId}' not supplied during template instantiation");
	}

	public sealed record AssetSource<T>(string AssetId) : ValueSource<T>;

	/// <summary>A transform property (position/rotation/scale) sourced from an entity by id via the
	/// <c>!entity</c> tag (resolved live each read). <typeparamref name="T"/> must be <c>Vector3</c>.
	/// When the id was written as <c>!parameter &lt;name&gt;</c>, <see cref="EntityIdParameter"/> holds
	/// that parameter name and <see cref="EntityId"/> is empty until <see cref="SubstituteParameters"/>
	/// fills in the resolved entity id at template instantiation.</summary>
	public sealed record EntityPropertySource<T>(string EntityId, EntityProperty Property, string? EntityIdParameter = null)
		: ValueSource<T>
	{
		public override ValueSource<T> SubstituteParameters(TransformContext ctx) =>
			EntityIdParameter is { } param
				? ctx.Parameters.TryGetValue(param, out var raw) && raw is StringValue sv
					? this with { EntityId = sv.Value, EntityIdParameter = null }
					: throw new ParsingException(
						$"!entity Id parameter '{param}' is missing or not a string during template instantiation")
				: this;
	}

	/// <summary>A physics property (velocity/angular velocity/position) sourced from an entity's
	/// <c>Rigidbody</c> by id via the <c>!rigidbody</c> tag (resolved live each read).
	/// <typeparamref name="T"/> must be <c>Vector3</c>.</summary>
	public sealed record RigidbodyPropertySource<T>(string EntityId, RigidbodyProperty Property) : ValueSource<T>;

	public sealed record TriggerOutputSource<T>(string OutputName) : ValueSource<T>;

	/// <summary>A <c>!if</c> conditional value: yields <see cref="Then"/> when <see cref="Condition"/> reads
	/// true, otherwise <see cref="Else"/>. The condition and both branches are resolved live each read, and
	/// only the selected branch is read (matching C# ternary short-circuiting). Lets a setter pick between two
	/// values in one place rather than via two condition-gated setters.</summary>
	public sealed record ConditionalSource<T>(
		ValueSource<bool> Condition,
		ValueSource<T> Then,
		ValueSource<T> Else) : ValueSource<T>
	{
		public override ValueSource<T> SubstituteParameters(TransformContext ctx) =>
			new ConditionalSource<T>(
				Condition.SubstituteParameters(ctx),
				Then.SubstituteParameters(ctx),
				Else.SubstituteParameters(ctx));
	}

	/// <summary>Properties exposed by the game clock to descriptor expressions via the <c>!clock</c> tag.</summary>
	public enum ClockProperty
	{
		DeltaTime,
		Time,
		FrameCount,
		UnscaledDeltaTime
	}

	/// <summary>A value sourced from the injected game clock (resolved each frame). See <c>!clock</c>.</summary>
	public sealed record ClockValueSource<T>(ClockProperty Property) : ValueSource<T>;

	/// <summary>The spatial query verbs exposed to descriptors via the <c>!query</c> tag. Extend by adding a
	/// case here plus an arm in <c>QueryValueProvider</c> — no new YAML tag is needed.</summary>
	public enum QueryKind
	{
		/// <summary>Id of the nearest entity with the tag within range (string; empty when none).</summary>
		NearestId,

		/// <summary>Position of the nearest entity with the tag within range (Vector3; the origin point when none).</summary>
		NearestPosition
	}

	/// <summary>A <c>!query</c> spatial lookup resolved live each read against the entity query service.
	/// <typeparamref name="T"/> is constrained at parse time to match <see cref="Kind"/> (string for an id
	/// query, Vector3 for a position query). <see cref="EntityTag"/> is the entity tag to search for;
	/// <see cref="Origin"/> is the point distances are measured from.</summary>
	public sealed record QuerySource<T>(
		QueryKind Kind,
		string EntityTag,
		ValueSource<Vector3> Origin,
		ValueSource<float> MaxRange) : ValueSource<T>
	{
		public override ValueSource<T> SubstituteParameters(TransformContext ctx) =>
			new QuerySource<T>(Kind, EntityTag, Origin.SubstituteParameters(ctx), MaxRange.SubstituteParameters(ctx));
	}

	/// <summary>
	/// A localised string sourced from the string table via a <c>!text</c> key. <see cref="Arguments"/>
	/// fill the localised template's <c>string.Format</c> placeholders (<c>{0}</c>, <c>{1}</c>, …) at
	/// runtime. Mirrors <see cref="ExpressionSource{T}"/> in owning an id plus a runtime argument list.
	/// </summary>
	public sealed record LocalisedTextSource<T>(
		string Key,
		IReadOnlyList<IValueSourceArg> Arguments) : ValueSource<T>
	{
		public override ValueSource<T> SubstituteParameters(TransformContext ctx) =>
			new LocalisedTextSource<T>(Key,
				Arguments.Select(a => a.SubstituteParameters(ctx)).ToArray());
	}
}
