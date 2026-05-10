using System.Collections.Generic;

namespace Assembler.Parsing.Phase2.Info
{
	public record EntityInfo(
		string Id,
		IReadOnlyList<string> Tags,
		ValueWrapper<Vector3> InitialPosition,
		ValueWrapper<Vector3> InitialRotation,
		IReadOnlyList<BehaviourInfo> Behaviours);
}