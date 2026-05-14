using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours.Spawners;
using Assembler.Core;
using Assembler.Extensions;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Behaviours
{
	public static class GameEntityFactory
	{
		public static void Create(EntityInfo entityInfo,
			VariableRegistry variableRegistry,
			CompiledExpressionsRegistry compiledExpressionsRegistry,
			IEntitySpawner entitySpawner,
			Dictionary<BehaviourDescriptor, GameBehaviour> behaviourRegistry,
			List<Action<IReadOnlyDictionary<BehaviourDescriptor, GameBehaviour>>> initialisations)
		{
			var gameObject = new GameObject(entityInfo.Id)
			{
				transform =
				{
					position = entityInfo.InitialPosition.Resolve(variableRegistry, compiledExpressionsRegistry).Value,
					rotation = entityInfo.InitialRotation.Resolve(variableRegistry, compiledExpressionsRegistry).Value.FromEuler()
				}
			};

			var gameEntity = gameObject.AddComponent<GameEntity>();
			gameEntity.Tags = entityInfo.Tags.ToArray();

			foreach (var behaviourInfo in entityInfo.Behaviours)
			{
				var (gameBehaviour, initialise) = GameBehaviourFactory.Create(gameObject,
					behaviourInfo,
					variableRegistry,
					compiledExpressionsRegistry,
					entitySpawner);

				behaviourRegistry.Add(new BehaviourDescriptor(entityInfo.Id, behaviourInfo.Id), gameBehaviour);
				initialisations.Add(initialise);
			}
		}
	}
}