using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assembler.Compiler.Compiler;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using UnityEditor;
using Vector3 = UnityEngine.Vector3;

namespace Assembler.Building
{
	public static class Builder
	{
		[MenuItem("Test/Build")]
		public static void TestBuild()
		{
			var yaml = File.ReadAllText("Assets/GameDescriptors/Pong.yaml");

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

			var variableRegistry = new VariableRegistry();

			foreach (var variableInfo in gameInfo.Variables)
			{
				variableRegistry.Register(variableInfo);
			}

			var compiledExpressionsRegistry = new CompiledExpressionsRegistry(typeRegistry, new ExpressionMethodCompiler());

			foreach (var expressionInfo in gameInfo.Expressions)
			{
				compiledExpressionsRegistry.CompileAndRegister(expressionInfo);
			}

			// 2. Load assets
			var assetRegistry = new AssetRegistry();
			assetRegistry.LoadAll(gameInfo.Assets);

			// 3. Instantiate Entities and Behaviours
			var behaviourRegistry = new BehaviourRegistry();

			var templatesById = gameInfo.Templates.ToDictionary(t => t.Id, t => t);

			var triggerContext = new TriggerContext();

			var gameEntityFactory = new GameEntityFactory(
				variableRegistry,
				compiledExpressionsRegistry,
				behaviourRegistry,
				assetRegistry,
				templatesById,
				gameInfo.Variables,
				triggerContext);
			
			var initialisations = new InitialisationQueue();

			foreach (var entityInfo in gameInfo.Entities)
			{
				var result = gameEntityFactory.Create(entityInfo);
				behaviourRegistry.Register(result);
				initialisations.Enqueue(result);
			}

			// 4. Initialise Behaviours
			initialisations.ExecuteAll(behaviourRegistry);

			// 5. Run game over condition
		}
	}
}