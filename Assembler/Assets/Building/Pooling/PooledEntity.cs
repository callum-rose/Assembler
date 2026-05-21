using System.Collections.Generic;
using Assembler.Behaviours;

namespace Assembler.Building.Pooling
{
	public sealed class PooledEntity
	{
		public GameEntity Entity { get; }
		public IReadOnlyList<GameBehaviour> Behaviours { get; }

		public PooledEntity(GameEntity entity, IReadOnlyList<GameBehaviour> behaviours)
		{
			Entity = entity;
			Behaviours = behaviours;
		}
	}
}
