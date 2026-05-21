using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info
{
	public abstract record ValueSource<T>;

	public sealed record None<T> : ValueSource<T>
	{
		public readonly static None<T> Instance = new();
	}

	public sealed record ConstantSource<T>(T Value) : ValueSource<T>;

	public sealed record ValueReferenceSource<T>(string VariableId) : ValueSource<T>;

	public sealed record ExpressionSource<T>(
		string ExpressionId,
		IReadOnlyList<ValueSource<object>> Arguments) : ValueSource<T>;
	
	public sealed record ParameterSource<T>(string ParameterId) : ValueSource<T>;

	public sealed record AssetSource<T>(string AssetId) : ValueSource<T>;

	public sealed record EntityPositionSource(string EntityId) : ValueSource<Vector3>;

	public sealed record TriggerOutputSource<T>(string OutputName) : ValueSource<T>;
}