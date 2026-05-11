using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assembler.Core;
using Assembler.Extensions;
using Assembler.Parsing.Phase1;
using Assembler.Parsing.Phase2;
using Assembler.Parsing.Phase2.Info;
using Assembler.Parsing.Phase3;
using UnityEditor;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Assembler.Building
{
	public static class Builder
	{
		[MenuItem("Test/Build")]
		public static void TestBuild()
		{
			var yaml = File.ReadAllText("Assets/Building/Pong.yaml");

			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			Build(gameInfo);
		}

		public static void Build(GameInfo gameInfo)
		{
			// 1. Initialize variables and expressions
			var typeRegistry = new Dictionary<string, Type>
			{
				["float"] = typeof(float),
				["int"] = typeof(int),
				["string"] = typeof(string),
				["bool"] = typeof(bool),
				["vector"] = typeof(Vector3)
			};

			var (variableRegistry, compiledExpressionsRegistry) = GameInitialiser.Initialise(gameInfo, typeRegistry);

			// 2. Apply Physics settings
			Physics.gravity = gameInfo.Physics.Gravity;

			var entityRegistry = new Dictionary<string, GameEntity>();
			var behaviourRegistry = new BehaviourRegistry();

			var initialisations = new List<Action>();

			// 3. Instantiate Entities and Add Behaviours
			foreach (var entityInfo in gameInfo.Entities)
			{
				var gameObject = new GameObject(entityInfo.Id)
				{
					transform =
					{
						position = entityInfo.InitialPosition.Resolve(variableRegistry, compiledExpressionsRegistry).Value,
						rotation = entityInfo.InitialRotation.Resolve(variableRegistry, compiledExpressionsRegistry).Value
							.FromEuler()
					}
				};

				var gameEntity = gameObject.AddComponent<GameEntity>();
				gameEntity.Tags = entityInfo.Tags.ToArray();

				entityRegistry[entityInfo.Id] = gameEntity;

				foreach (var behaviourInfo in entityInfo.Behaviours)
				{
					var (gameBehaviour, initialise) = GameBehaviourFactory.AddComponent(gameObject,
						behaviourInfo,
						variableRegistry,
						compiledExpressionsRegistry);

					behaviourRegistry.Register(new BehaviourDescriptor(entityInfo.Id, behaviourInfo.Id), gameBehaviour);
					initialisations.Add(initialise);
				}
			}

			// 4. Initialise Behaviours
			foreach (var initialise in initialisations)
			{
				initialise();
			}
		}
	}

}