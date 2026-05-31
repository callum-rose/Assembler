using System.IO;
using System.Linq;
using Assembler.Compiler.Compiler;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Resolving;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Assembler.Building
{
	public static class Builder
	{
#if UNITY_EDITOR
		[MenuItem("Test/Build Pong")]
		public static void BuildPong()
		{
			var yaml = File.ReadAllText("Assets/ExampleGameDescriptors/Pong.yaml");

			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			Build(gameInfo);
		}

		[MenuItem("Test/Build Snake")]
		public static void BuildSnake()
		{
			var yaml = File.ReadAllText("Assets/ExampleGameDescriptors/Snake 2.yaml");

			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			Build(gameInfo);
		}

		[MenuItem("Test/Build EnemyHealthDemo")]
		public static void BuildEnemyHealthDemo()
		{
			var yaml = File.ReadAllText("Assets/ExampleGameDescriptors/EnemyHealthDemo.yaml");

			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			Build(gameInfo);
		}

		[MenuItem("Test/Build SpawnedBubblesDemo")]
		public static void BuildSpawnedBubblesDemo()
		{
			var yaml = File.ReadAllText("Assets/ExampleGameDescriptors/SpawnedBubblesDemo.yaml");

			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			Build(gameInfo);
		}

		[MenuItem("Test/Build TaggedListenerDemo")]
		public static void BuildTaggedListenerDemo()
		{
			var yaml = File.ReadAllText("Assets/ExampleGameDescriptors/TaggedListenerDemo.yaml");

			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			Build(gameInfo);
		}		
		
		[MenuItem("Test/Build Asteroids")]
		public static void BuildAsteroids()
		{
			var yaml = File.ReadAllText("Assets/ExampleGameDescriptors/Asteroids.yaml");

			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			Build(gameInfo);
		}

		[MenuItem("Test/Build AnimationsDemo")]
		public static void BuildAnimationsDemo()
		{
			var yaml = File.ReadAllText("Assets/ExampleGameDescriptors/AnimationsDemo.yaml");

			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			Build(gameInfo);
		}

		[MenuItem("Test/Build HierarchyDemo")]
		public static void BuildHierarchyDemo()
		{
			var yaml = File.ReadAllText("Assets/ExampleGameDescriptors/HierarchyDemo.yaml");

			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			Build(gameInfo);
		}

		[MenuItem("Test/Build EntityPositionDemo")]
		public static void BuildEntityPositionDemo()
		{
			var yaml = File.ReadAllText("Assets/ExampleGameDescriptors/EntityPositionDemo.yaml");

			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			Build(gameInfo);
		}

		[MenuItem("Test/Build FlappyBird")]
		public static void BuildFlappyBird()
		{
			var yaml = File.ReadAllText("Assets/ExampleGameDescriptors/FlappyBird.yaml");

			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			Build(gameInfo);
		}

		[MenuItem("Test/Build DoodleJump")]
		public static void BuildDoodleJump()
		{
			var yaml = File.ReadAllText("Assets/ExampleGameDescriptors/DoodleJump.yaml");

			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			Build(gameInfo);
		}
#endif

		public static void Build(GameInfo gameInfo)
		{
			// 1. Initialize variables and expressions
			var typeRegistry = BuiltInTypeRegistry.Default;

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
			var entityTransformRegistry = new EntityTransformRegistry();
			var exclusiveGroupRegistry = new ExclusiveGroupRegistry();

			var templatesById = gameInfo.Templates.ToDictionary(t => t.Id, t => t);

			var gameEntityFactory = new GameEntityFactory(
				variableRegistry,
				compiledExpressionsRegistry,
				behaviourRegistry,
				assetRegistry,
				entityTransformRegistry,
				exclusiveGroupRegistry,
				templatesById,
				gameInfo.ParseContext);

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