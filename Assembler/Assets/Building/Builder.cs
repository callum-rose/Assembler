using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assembler.Compiler.Compiler;
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
			var yaml = File.ReadAllText("Assets/Building/Snake.yaml");

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

			// 3. Instantiate Entities and Behaviours
			var behaviourRegistry = new Dictionary<BehaviourDescriptor, GameBehaviour>();
			var initialisations = new List<Action<IReadOnlyDictionary<BehaviourDescriptor, GameBehaviour>>>();

			foreach (var entityInfo in gameInfo.Entities)
			{
				GameEntityFactory.Create(entityInfo, variableRegistry, compiledExpressionsRegistry, behaviourRegistry, initialisations);
			}

			// 4. Initialise Behaviours
			foreach (var initialise in initialisations)
			{
				initialise(behaviourRegistry);
			}

			// 5. Run game over condition
		}
	}
}