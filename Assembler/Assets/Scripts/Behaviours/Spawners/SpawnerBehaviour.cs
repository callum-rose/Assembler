using Assembler.Core;
using Assembler.Parsing.Phase3;

namespace Assembler.Behaviours.Spawners
{
	public class SpawnerBehaviour : GameBehaviour<SpawnerData>
	{
		public IEntitySpawner Spawner { get; set; }

		public override void Execute()
		{
			Spawner.Spawn(Data.TemplateId.Value, Data.Position.Value);
			NotifyListeners();
		}
	}
}
