using System.Collections.Generic;

namespace Assembler.Parsing.Info
{
	public record GameInfo(
		AboutInfo About,
		WorldInfo World,
		PhysicsInfo Physics,
		IReadOnlyList<AssetInfo> Assets,
		IReadOnlyList<ValueInfo> Variables,
		IReadOnlyList<ExpressionInfo> Expressions,
		IReadOnlyList<EntityInfo> Templates,
		IReadOnlyList<ConcreteEntityInfo> Entities,
		ValueSource<bool> GameOverCondition);

}