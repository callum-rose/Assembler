using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info
{
	public sealed record ChildEntityInfo(
		string IdSuffix,
		string? TemplateRefId,
		IReadOnlyDictionary<string, AssemblerValue> Parameters,
		IReadOnlyList<string> Tags,
		ValueSource<Vector3> InitialPosition,
		ValueSource<Vector3> InitialRotation,
		IReadOnlyList<BehaviourInfo> Behaviours,
		IReadOnlyList<ValueInfo> Variables,
		IReadOnlyList<ChildEntityInfo> Children);
}
