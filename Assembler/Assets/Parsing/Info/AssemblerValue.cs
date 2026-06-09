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

	/// <summary>A transform property exposed by the <c>!entity { Id, Property }</c> tag.</summary>
	public enum EntityProperty
	{
		Position,
		Rotation,
		Scale
	}

	/// <summary>A <c>!entity { Id, Property }</c> reference — reads a transform property (position,
	/// rotation, scale) off an entity by id at runtime. Resolves to a live <c>Vector3</c>.</summary>
	public sealed record EntityPropertyRef(string Id, EntityProperty Property) : AssemblerRef(Id);

	/// <summary>A physics property exposed by the <c>!rigidbody { Id, Property }</c> tag.</summary>
	public enum RigidbodyProperty
	{
		Velocity,
		AngularVelocity,
		Position
	}

	/// <summary>A <c>!rigidbody { Id, Property }</c> reference — reads a physics property (velocity,
	/// angular velocity, position) off an entity's <c>Rigidbody</c> by id at runtime. Resolves to a
	/// live <c>Vector3</c> (zero when the entity has no rigidbody).</summary>
	public sealed record RigidbodyPropertyRef(string Id, RigidbodyProperty Property) : AssemblerRef(Id);

	/// <summary>A <c>!clock &lt;property&gt;</c> reference (e.g. <c>!clock deltaTime</c>), resolved at
	/// runtime against the injected game clock. Carries the requested property name verbatim.</summary>
	public sealed record ClockRef(string Property) : AssemblerValue;

	public sealed record OutputRef(string Id) : AssemblerRef(Id);

	public sealed record ParamRef(string Id) : AssemblerRef(Id);

	/// <summary>A <c>!expr { Do, With }</c> call site. <see cref="Do"/> is either a declared
	/// expression's name/alias (a named call) or an anonymous inline C# body; <see cref="With"/>
	/// carries the operands. The remaining fields are optional hints for an inline body
	/// (return type, per-operand types, and extra types / static-method sources); they are
	/// ignored on a named call.</summary>
	public sealed record ExprRef(
		string Do,
		IReadOnlyList<AssemblerValue> With,
		string? ReturnType = null,
		IReadOnlyList<string>? ArgumentTypes = null,
		IReadOnlyList<string>? RegisterTypes = null,
		IReadOnlyList<string>? RegisterTypeStatics = null) : AssemblerValue;

	/// <summary>A <c>!text &lt;key&gt;</c> reference into the localisation string table. Carries the lookup
	/// key plus any runtime arguments that fill the localised template's <c>{0}</c>/<c>{1}</c> placeholders.</summary>
	public sealed record TextRef(string Key, IReadOnlyList<AssemblerValue> Arguments) : AssemblerValue;

	/// <summary>Marker produced by a nested <c>!gameover</c> tag (e.g. inside a state machine's
	/// <c>OnEnter</c>/<c>OnExit</c> hook list). Top-level <c>Listeners:</c> handle <c>!gameover</c>
	/// directly via <see cref="Assembler.Parsing.Info.GameOverListenerInfo"/>; nested occurrences
	/// flow through the property pipeline as this marker and are turned back into a
	/// <see cref="Assembler.Parsing.Info.GameOverListenerInfo"/> by the nested-listener parser.</summary>
	public sealed record GameOverMarker : AssemblerValue;

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
