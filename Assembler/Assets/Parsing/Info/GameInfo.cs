using System.Collections.Generic;

namespace Assembler.Parsing.Info
{
	public record GameInfo(
		AboutInfo About,
		WorldInfo World,
		PhysicsInfo Physics,
		IReadOnlyList<AssetInfo> Assets,
		IReadOnlyList<VariableInfo> Variables,
		IReadOnlyList<ExpressionInfo> Expressions,
		IReadOnlyList<EntityInfo> Templates,
		IReadOnlyList<EntityInfo> Entities,
		ValueSource<bool> GameOverCondition);

}