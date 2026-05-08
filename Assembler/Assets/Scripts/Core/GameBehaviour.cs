using System;
using System.Collections.Generic;
using Assembler.Parsing2.Dtos;
using UnityEngine;

namespace Core
{
	public static class GameBehaviourFactory
	{
		public static BehaviourDto Create(GameObject gameObject, BehaviourDto behaviourDto)
		{
			switch (behaviourDto.Type)
			{
				case "box collider":
					var boxCollider = gameObject.AddComponent<BoxCollider>();
					
					if (behaviourDto.Properties.TryGetValue("Size", out var value) && value is VecDto size)
					{
						var sizeVector = new Vector3(size.X, size.Y, size.Z);
						boxCollider.size = sizeVector;
					}

					break;
					
				case "":
				default:
					throw new Exception("Cannot create behaviour of type null or empty");
			}
		}
	}
	
	public abstract class GameBehaviour : MonoBehaviour
	{
		public abstract class Configuration
		{
		}
		
		protected GameEntity Entity { get; private set; }
		
		public void Initialise(GameEntity entity, Configuration configuration)
		{
			Entity = entity;

			OnInitialise(configuration);
		}

		public abstract void Execute();

		protected virtual void OnInitialise(Configuration configuration) { }
	}
}