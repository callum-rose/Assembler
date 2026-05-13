using System.Collections.Generic;

namespace Assembler.Parsing.Phase2.Info
{
	public record GameInfo(
		AboutInfo About,
		WorldInfo World,
		PhysicsInfo Physics,
		// IReadOnlyList<ValueInfo> Constants,
		IReadOnlyList<VariableInfo> Variables,
		IReadOnlyList<ExpressionInfo> Expressions,
		IReadOnlyList<EntityInfo> Templates,
		IReadOnlyList<EntityInfo> Entities,
		ValueSource<bool> GameOverCondition);
}