using Assembler.Parsing2.Dtos;
using UnityEngine;

namespace Core
{
	public class GameEntityFactory
	{
		public static GameEntity CreateEntity(EntityDto entityDto)
		{
			var gameObject = new GameObject();
			var gameEntity = gameObject.AddComponent<GameEntity>();
			gameEntity.Initialise(entityDto);
			return gameEntity;
		}
	}
}