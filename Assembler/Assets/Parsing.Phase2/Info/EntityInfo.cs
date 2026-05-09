using System.Collections.Generic;

namespace Assembler.Parsing2.Info
{
	public record EntityInfo(
		string Id,
		IReadOnlyList<string> Tags,
		Vector3 InitialPosition,
		Vector3 InitialRotation,
		IReadOnlyList<BehaviourInfo> Behaviours);
}