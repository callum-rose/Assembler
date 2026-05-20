using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Behaviours.Spawners
{
	public interface IEntitySpawner
	{
		void Spawn(string templateId, Vector3 position, Vector3 rotation, IReadOnlyDictionary<string, object> parameters);
		void Despawn(GameEntity entity);
	}
}
