using System.Linq;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;

namespace Tests.Parsing
{
	public class DynamicVariableTests
	{
		[Test]
		public void EntityVariableScope_ShadowsGlobalRegistry()
		{
			var registry = new VariableRegistry();
			registry.Register(new ValueInfo("health", new IntValue(50)));

			var scope = new EntityVariableScope();
			scope.Create(new ValueInfo("health", new IntValue(100)));

			Assert.AreEqual(100, registry.Get<int>("health", scope).Value);
			Assert.AreEqual(50, registry.Get<int>("health").Value);
			Assert.AreEqual(50, registry.Get<int>("health", new EntityVariableScope()).Value);
		}

		[Test]
		public void EntityVariableScope_FallsBackToGlobalForUnknownIds()
		{
			var registry = new VariableRegistry();
			registry.Register(new ValueInfo("speed", new FloatValue(2.5f)));

			var scope = new EntityVariableScope();
			scope.Create(new ValueInfo("health", new IntValue(10)));

			Assert.AreEqual(2.5f, registry.Get<float>("speed", scope).Value);
		}

		[Test]
		public void EntityVariableScope_TwoScopesAreIndependent()
		{
			var registry = new VariableRegistry();

			var a = new EntityVariableScope();
			a.Create(new ValueInfo("health", new IntValue(1)));

			var b = new EntityVariableScope();
			b.Create(new ValueInfo("health", new IntValue(2)));

			var aHealth = registry.Get<int>("health", a);
			var bHealth = registry.Get<int>("health", b);

			aHealth.Value = 99;

			Assert.AreEqual(99, registry.Get<int>("health", a).Value);
			Assert.AreEqual(2, registry.Get<int>("health", b).Value);
			Assert.AreNotSame(aHealth, bHealth);
		}

		[Test]
		public void EntityVariableScope_DisposeClearsLocals()
		{
			var registry = new VariableRegistry();
			registry.Register(new ValueInfo("health", new IntValue(7)));

			var scope = new EntityVariableScope();
			scope.Create(new ValueInfo("health", new IntValue(99)));

			scope.Dispose();

			Assert.AreEqual(7, registry.Get<int>("health", scope).Value);
		}

		[Test]
		public void Templates_VariablesBlockIsParsedOntoEntityInfo()
		{
			var yaml = @"
Templates:
  - Id: enemy
    Variables:
      - Id: health
        Value: 100
      - Id: speed
        Value: !parameter spawn_speed
Entities:
  - Id: goblin
    Template: enemy
    Parameters:
      spawn_speed: 3
";

			var parser = new GameFileParser();
			var gameDto = parser.Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);

			var goblin = gameInfo.Entities.Single();

			Assert.AreEqual(2, goblin.Variables.Count);

			var health = goblin.Variables.Single(v => v.Id == "health");
			Assert.IsInstanceOf<IntValue>(health.Value);
			Assert.AreEqual(100, ((IntValue)health.Value).Value);

			var speed = goblin.Variables.Single(v => v.Id == "speed");
			Assert.IsInstanceOf<IntValue>(speed.Value);
			Assert.AreEqual(3, ((IntValue)speed.Value).Value);
		}
	}
}