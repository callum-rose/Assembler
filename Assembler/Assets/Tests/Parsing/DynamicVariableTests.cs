using System.Collections.Generic;
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
		public void StringifiedValueResolver_BindsIntVariableAsLiveString()
		{
			// Models the text-label use-case: an int variable (e.g. `score`) bound
			// directly into a text-label.Text slot (typed as string) without an
			// intermediate string variable. The resolved provider must reflect
			// live mutations to the int.
			var values = new List<ValueInfo>
			{
				new("score", new IntValue(0))
			};

			var registry = new VariableRegistry();
			registry.Register(values[0]);

			var raw = new VarRef("score");

			var provider = StringifiedValueResolver.Resolve(
				raw,
				values,
				registry,
				expressions: null,
				assets: null,
				triggerContext: null,
				scope: new EntityVariableScope(),
				entityTransforms: null);

			Assert.AreEqual("0", provider.Value);

			registry.Get<int>("score").Value = 42;
			Assert.AreEqual("42", provider.Value, "Provider should reflect live updates to the int variable.");

			registry.Get<int>("score").Value = -7;
			Assert.AreEqual("-7", provider.Value);
		}

		[Test]
		public void StringifiedValueResolver_PassesThroughStringConstants()
		{
			var values = new List<ValueInfo>();
			var registry = new VariableRegistry();

			var provider = StringifiedValueResolver.Resolve(
				new StringValue("hello"),
				values,
				registry,
				expressions: null,
				assets: null,
				triggerContext: null,
				scope: new EntityVariableScope(),
				entityTransforms: null);

			Assert.AreEqual("hello", provider.Value);
		}

		[Test]
		public void StringifiedValueResolver_StringifiesFloatConstants()
		{
			var values = new List<ValueInfo>();
			var registry = new VariableRegistry();

			var provider = StringifiedValueResolver.Resolve(
				new FloatValue(3.5f),
				values,
				registry,
				expressions: null,
				assets: null,
				triggerContext: null,
				scope: new EntityVariableScope(),
				entityTransforms: null);

			Assert.AreEqual(3.5f.ToString(), provider.Value);
		}

		[Test]
		public void TextLabelInfo_ParsesNonStringTextProperty()
		{
			var yaml = @"
Variables:
  score: 0
Entities:
  hud:
    Behaviours:
      score_label:
        Type: text label
        Properties:
          Text: !var score
          Label: ""Score: ""
          FontSize: 24
          Rect: { X: 0, Y: 0, Width: 200, Height: 32 }
";

			var parser = new GameFileParser();
			var gameDto = parser.Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);

			var hud = gameInfo.Entities.Single();
			var label = hud.Behaviours.OfType<Assembler.Parsing.Info.Behaviours.TextLabelInfo>().Single();

			// The Text slot now carries the raw AssemblerValue so the factory can
			// determine the inner type at resolve time and stringify if necessary.
			Assert.IsInstanceOf<VarRef>(label.Text);
			Assert.AreEqual("score", ((VarRef)label.Text).Id);
		}

		[Test]
		public void Templates_VariablesBlockIsParsedOntoEntityInfo()
		{
			var yaml = @"
Templates:
  enemy:
    Variables:
      health: 100
      speed: !parameter spawn_speed
Entities:
  goblin:
    Template:
      Id: enemy
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