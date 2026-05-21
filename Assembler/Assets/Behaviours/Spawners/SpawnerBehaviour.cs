using System.Collections.Generic;
using System.Linq;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Spawners
{
	/// <summary>Spawns an instance of a named template at a position when Executed.</summary>
	/// <remarks>
	/// Properties:
	///   TemplateId: Id of the template to instantiate.
	///   Position: World-space position for the spawned entity.
	///   Rotation: Euler rotation in degrees for the spawned entity.
	///   Parameters: Optional name→value overrides forwarded to the template's parameter slots.
	/// </remarks>
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