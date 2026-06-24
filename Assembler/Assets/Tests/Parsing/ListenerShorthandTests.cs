using System.Linq;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using NUnit.Framework;

namespace Tests.Parsing
{
	public class ListenerShorthandTests
	{
		private static GameInfo Parse(string yaml) =>
			Transformer.Transform(new GameFileParser().Parse(yaml));

		private static DirectListenerInfo DirectListener(GameInfo info, string entityId, string behaviourId) =>
			(DirectListenerInfo)info.Entities
				.First(e => e.Id == entityId)
				.Behaviours
				.First(b => b.Id == behaviourId)
				.Listeners
				.Single();

		[Test]
		public void OmittedEntityIdDefaultsToEnclosingEntity()
		{
			var info = Parse(@"
Entities:
  player:
    Behaviours:
      trigger:
        Type: input action
        Properties: { Action: jump }
        Listeners:
          - BehaviourId: move
      move:
        Type: translate
        Properties: { Displacement: !vec { X: 1, Y: 0, Z: 0 } }
");
			var listener = DirectListener(info, "player", "trigger");

			Assert.AreEqual("player", listener.EntityId.Id);
			Assert.AreEqual("move", listener.BehaviourId);
		}

		[Test]
		public void ScalarShorthandBehaviourTargetsOwnEntity()
		{
			var info = Parse(@"
Entities:
  player:
    Behaviours:
      trigger:
        Type: input action
        Properties: { Action: jump }
        Listeners: [ move ]
      move:
        Type: translate
        Properties: { Displacement: !vec { X: 1, Y: 0, Z: 0 } }
");
			var listener = DirectListener(info, "player", "trigger");

			Assert.AreEqual("player", listener.EntityId.Id);
			Assert.AreEqual("move", listener.BehaviourId);
		}

		[Test]
		public void ScalarShorthandEntitySlashBehaviourTargetsNamedEntity()
		{
			var info = Parse(@"
Entities:
  player:
    Behaviours:
      trigger:
        Type: input action
        Properties: { Action: jump }
        Listeners: [ ""score board / increment"" ]
  score board:
    Behaviours:
      increment:
        Type: translate
        Properties: { Displacement: !vec { X: 0, Y: 1, Z: 0 } }
");
			var listener = DirectListener(info, "player", "trigger");

			Assert.AreEqual("score board", listener.EntityId.Id);
			Assert.AreEqual("increment", listener.BehaviourId);
		}

		[Test]
		public void ExplicitEntityIdStillWorks()
		{
			var info = Parse(@"
Entities:
  player:
    Behaviours:
      trigger:
        Type: input action
        Properties: { Action: jump }
        Listeners:
          - EntityId: score board
            BehaviourId: increment
  score board:
    Behaviours:
      increment:
        Type: translate
        Properties: { Displacement: !vec { X: 0, Y: 1, Z: 0 } }
");
			var listener = DirectListener(info, "player", "trigger");

			Assert.AreEqual("score board", listener.EntityId.Id);
			Assert.AreEqual("increment", listener.BehaviourId);
		}

		[Test]
		public void OmittedEntityIdInTemplateResolvesToInstanceId()
		{
			var info = Parse(@"
Templates:
  mover:
    Behaviours:
      trigger:
        Type: input action
        Properties: { Action: jump }
        Listeners:
          - BehaviourId: move
      move:
        Type: translate
        Properties: { Displacement: !vec { X: 1, Y: 0, Z: 0 } }
Entities:
  hero:
    Template: { Id: mover }
");
			var listener = DirectListener(info, "hero", "trigger");

			Assert.AreEqual("hero", listener.EntityId.Id);
			Assert.AreEqual("move", listener.BehaviourId);
		}

		[Test]
		public void OutputsPreservedInMappingForm()
		{
			var info = Parse(@"
Entities:
  player:
    Behaviours:
      hit:
        Type: trigger enter trigger
        Properties: { TagsToDetect: [ wall ] }
        Listeners:
          - BehaviourId: react
            Outputs:
              contact_point: hit_point
      react:
        Type: translate
        Properties: { Displacement: !vec { X: 0, Y: 0, Z: 0 } }
");
			var listener = DirectListener(info, "player", "hit");

			Assert.AreEqual("player", listener.EntityId.Id);
			Assert.AreEqual("hit_point", listener.OutputMapping["contact_point"]);
		}
	}
}
