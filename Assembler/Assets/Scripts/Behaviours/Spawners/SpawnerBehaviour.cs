using Assembler.Core;
using Assembler.Parsing.Phase3;

namespace Assembler.Behaviours.Spawners
{
	public class SpawnerBehaviour : GameBehaviour<SpawnerData>
	{
		public VariableRegistry Variables { get; set; }
		public CompiledExpressionsRegistry ExpressionRegistry { get; set; }

		public override void Execute()
		{
			GameEntityFactory.Create(Data.Entity, );
		}
	}
}