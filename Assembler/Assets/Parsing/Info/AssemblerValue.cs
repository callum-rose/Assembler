using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info
{
	public abstract record AssemblerValue;

	public sealed record NoValue : AssemblerValue
	{
		public readonly static NoValue Instance = new();
	}

	public sealed record IntValue(int Value) : AssemblerValue;

	public sealed record FloatValue(float Value) : AssemblerValue;

	public sealed record BoolValue(bool Value) : AssemblerValue;

	public sealed record StringValue(string Value) : AssemblerValue;

	public sealed record Vector2Value(Vector2 Value) : AssemblerValue;

	public sealed record Vector3Value(Vector3 Value) : AssemblerValue;

	public sealed record ColorValue(Color Value) : AssemblerValue;

	public sealed record VecValue(AssemblerValue X, AssemblerValue Y, AssemblerValue Z) : AssemblerValue;

	public sealed record ColourValue(
		AssemblerValue R,
		AssemblerValue G,
		AssemblerValue B,
		AssemblerValue A,
		AssemblerValue Raw) : AssemblerValue;

	public abstract record AssemblerRef(string Id) : AssemblerValue;

	public sealed record VarRef(string Id) : AssemblerRef(Id);

	public sealed record AssetRef(string Id) : AssemblerRef(Id);

	public sealed record EntityPositionRef(string Id) : AssemblerRef(Id);

	/// <summary>A <c>!clock &lt;property&gt;</c> reference (e.g. <c>!clock deltaTime</c>), resolved at
	/// runtime against the injected game clock. Carries the requested property name verbatim.</summary>
	public sealed record ClockRef(string Property) : AssemblerValue;

	public sealed record OutputRef(string Id) : AssemblerRef(Id);

	public sealed record ParamRef(string Id) : AssemblerRef(Id);

	public sealed record ExprRef(string ExpressionId, IReadOnlyList<AssemblerValue> Arguments) : AssemblerValue;

	public sealed record DictValue(IReadOnlyDictionary<string, AssemblerValue> Value) : AssemblerValue;

	public sealed record ListValue(IReadOnlyList<AssemblerValue> Value) : AssemblerValue;

	/// <summary>
	/// A list value with a known element CLR type — produced by typed YAML tags
	/// like <c>!vec []</c>, <c>!colour []</c>, <c>!int []</c>, <c>!float []</c>,
	/// <c>!bool []</c>, <c>!string []</c>. Items are pre-resolved primitive
	/// AssemblerValues (e.g. <see cref="Vector3Value"/>, <see cref="IntValue"/>)
	/// so the variable registry can allocate a typed backing list (<c>List&lt;T&gt;</c>)
	/// without having to infer element types.
	/// </summary>
	public sealed record TypedListValue(Type ElementType, IReadOnlyList<AssemblerValue> Items) : AssemblerValue;
}
