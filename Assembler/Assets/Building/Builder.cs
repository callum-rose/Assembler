using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assembler.Parsing.Phase2.Parsing.Phase2;
using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using Assembler.Parsing.Phase3.Parsing.Phase3;
using AssemblerAlpha.Core;
using Parsing.Phase1;
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
				{
					"float", typeof(float)
				},
				{
					"int", typeof(int)
				},
				{
					"string", typeof(string)
				},
				{
					"bool", typeof(bool)
				},
				{
					"vector", typeof(Vector3)
				}
			};

			var (variableRegistry, compiledExpressionsRegistry) = GameInitialiser.Initialise(gameInfo, typeRegistry);

			// 2. Apply Physics settings
			Physics.gravity = gameInfo.Physics.Gravity.ToUnity();

			var entityRegistry = new Dictionary<string, (GameEntity, Dictionary<string, GameBehaviour>)>();

			// 3. Instantiate Entities and Add Behaviours
			var behavioursToInitialise = new List<(Component, BehaviourInfo)>();
			foreach (var entityInfo in gameInfo.Entities)
			{
				var gameObject = new GameObject(entityInfo.Id)
				{
					transform =
					{
						position = entityInfo.InitialPosition.ToUnity(),
						rotation = Quaternion.Euler(entityInfo.InitialRotation.ToUnity())
					}
				};

				var gameEntity = gameObject.AddComponent<GameEntity>();
				gameEntity.Tags = entityInfo.Tags?.ToArray() ?? Array.Empty<string>();

				var behaviourRegistry = new Dictionary<string, GameBehaviour>();
				entityRegistry.Add(entityInfo.Id, (gameEntity, behaviourRegistry));

				foreach (var behaviourInfo in entityInfo.Behaviours)
				{
					var component = GameBehaviourFactory.AddComponent(gameObject, behaviourInfo);
					behavioursToInitialise.Add((component, behaviourInfo));
					
					// behaviourRegistry[behaviourInfo.Id] = component;
				}
			}

			// 4. Initialise Behaviours
			foreach (var (component, behaviourInfo) in behavioursToInitialise)
			{
				GameBehaviourFactory.SetData(component, behaviourInfo);
			}
		}
	}
}