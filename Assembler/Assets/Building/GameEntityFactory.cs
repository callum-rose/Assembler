using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours.Spawners;
using Assembler.Core;
using Assembler.Extensions;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Building
{
	public class GameEntityFactory
	{
		private readonly VariableRegistry _variables;
		private readonly CompiledExpressionsRegistry _expressions;
		private readonly Dictionary<BehaviourDescriptor, GameBehaviour> _behaviourRegistry;
		private readonly AssetRegistry _assets;
		private readonly IEntitySpawner _entitySpawner;

		public GameEntityFactory(VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			Dictionary<BehaviourDescriptor, GameBehaviour> behaviourRegistry,
			AssetRegistry assets,
			IEntitySpawner entitySpawner)
		{
			_variables = variables;
			_expressions = expressions;
			_behaviourRegistry = behaviourRegistry;
			_assets = assets;
			_entitySpawner = entitySpawner;
		}

		public void Create(EntityInfo entityInfo,
			List<Action<IReadOnlyDictionary<BehaviourDescriptor, GameBehaviour>>> initialisations)
		{
			var gameObject = new GameObject(entityInfo.Id)
			{
				transform =
				{
					position = entityInfo.InitialPosition.Resolve(_variables, _expressions, _assets).Value,
					rotation = entityInfo.InitialRotation.Resolve(_variables, _expressions, _assets).Value.FromEuler()
				}
			};

			var gameEntity = gameObject.AddComponent<GameEntity>();
			gameEntity.Tags = entityInfo.Tags.ToArray();

			foreach (var behaviourInfo in entityInfo.Behaviours)
			{
				var (gameBehaviour, initialise) = GameBehaviourFactory.Create(gameObject,
					behaviourInfo,
					_variables,
					_expressions,
					_entitySpawner,
					_assets);

				_behaviourRegistry.Add(new BehaviourDescriptor(entityInfo.Id, behaviourInfo.Id), gameBehaviour);
				initialisations.Add(initialise);
			}
		}
	}
}