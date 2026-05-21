using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info
{
	public abstract record EntityInfo(
		string Id,
		EntityInfo Template,
		IReadOnlyList<string> Tags,
		ValueSource<Vector3> InitialPosition,
		ValueSource<Vector3> InitialRotation,
		IReadOnlyList<BehaviourInfo> Behaviours,
		IReadOnlyList<ValueInfo> Variables,
		IReadOnlyList<ChildEntityInfo> Children);

	public sealed record ConcreteEntityInfo(
		string Id,
		IReadOnlyList<string> Tags,
		ValueSource<Vector3> InitialPosition,
		ValueSource<Vector3> InitialRotation,
		IReadOnlyList<BehaviourInfo> Behaviours,
		IReadOnlyList<ValueInfo> Variables,
		IReadOnlyList<ChildEntityInfo> Children)
		: EntityInfo(Id, NullEntityInfo.Instance, Tags, InitialPosition, InitialRotation, Behaviours, Variables, Children);

	public sealed record NullEntityInfo() : EntityInfo(string.Empty,
		Instance,
		Array.Empty<string>(),
		None<Vector3>.Instance,
		None<Vector3>.Instance,
		Array.Empty<BehaviourInfo>(),
		Array.Empty<ValueInfo>(),
		Array.Empty<ChildEntityInfo>())
	{
		public readonly static NullEntityInfo Instance = new();
	}
}
