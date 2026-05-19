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

	public sealed record OutputRef(string Id) : AssemblerRef(Id);

	public sealed record ParamRef(string Id) : AssemblerRef(Id);

	public sealed record ExprRef(string ExpressionId, IReadOnlyList<AssemblerValue> Arguments) : AssemblerValue;

	public sealed record DictValue(IReadOnlyDictionary<string, AssemblerValue> Value) : AssemblerValue;

	public sealed record ListValue(IReadOnlyList<AssemblerValue> Value) : AssemblerValue;
}
