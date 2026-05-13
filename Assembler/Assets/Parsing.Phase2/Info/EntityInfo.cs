using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Phase2.Info
{
	public abstract record EntityInfo(
		string Id,
		EntityInfo Template,
		IReadOnlyList<string> Tags,
		ValueSource<Vector3> InitialPosition,
		ValueSource<Vector3> InitialRotation,
		IReadOnlyList<BehaviourInfo> Behaviours);

	public sealed record ConcreteEntityInfo(
		string Id,
		EntityInfo Template,
		IReadOnlyList<string> Tags,
		ValueSource<Vector3> InitialPosition,
		ValueSource<Vector3> InitialRotation,
		IReadOnlyList<BehaviourInfo> Behaviours) : EntityInfo(Id, Template, Tags, InitialPosition, InitialRotation, Behaviours);

	public sealed record NullEntityInfo() : EntityInfo(string.Empty,
		Instance,
		Array.Empty<string>(),
		None<Vector3>.Instance,
		None<Vector3>.Instance,
		Array.Empty<BehaviourInfo>())
	{
		public readonly static NullEntityInfo Instance = new();
	}
}