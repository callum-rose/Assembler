using System.Collections.Generic;
using System.Linq;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Spawners
{
	public class SpawnerBehaviour : GameBehaviour<SpawnerData>
	{
		public IEntitySpawner Spawner { get; set; }

		public override void Execute()
		{
			Spawner.Spawn(Data.TemplateId.Value,
				Data.Position.Value,
				Data.Rotation.Value,
				Data.Parameters.ToDictionary(kv => kv.Key, kv => kv.Value.Value));

			NotifyListeners();
		}
	}
}