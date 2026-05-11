using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Phase2.Info
{
	public record EntityInfo(
		string Id,
		IReadOnlyList<string> Tags,
		ValueSource<Vector3> InitialPosition,
		ValueSource<Vector3> InitialRotation,
		IReadOnlyList<BehaviourInfo> Behaviours);
}