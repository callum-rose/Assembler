using UnityEngine;

namespace Assembler.Behaviours.Spawners
{
	public interface IEntitySpawner
	{
		void Spawn(string templateId, Vector3 position);
	}
}
