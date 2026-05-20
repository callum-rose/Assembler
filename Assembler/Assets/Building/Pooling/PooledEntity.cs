using System.Collections.Generic;
using Assembler.Behaviours;
using UnityEngine;

namespace Assembler.Building.Pooling
{
	public sealed class PooledEntity
	{
		public GameObject GameObject { get; }
		public GameEntity Entity { get; }
		public IReadOnlyList<GameBehaviour> Behaviours { get; }

		public PooledEntity(GameObject gameObject, GameEntity entity, IReadOnlyList<GameBehaviour> behaviours)
		{
			GameObject = gameObject;
			Entity = entity;
			Behaviours = behaviours;
		}
	}
}
