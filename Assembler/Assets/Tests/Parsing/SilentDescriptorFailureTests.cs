using System.Linq;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;

namespace Tests.Parsing
{
	// Issue 237: three descriptor mistakes used to fail silently (or far downstream with CLR vocabulary).
	// These lock in the loud/correct behaviour at the parse boundary.
	public class SilentDescriptorFailureTests
	{
		private static GameInfo Transform(string yaml) => Transformer.Transform(new GameFileParser().Parse(yaml));

		private static BehaviourInfo Behaviour(GameInfo info, string entityId, string behaviourId) =>
			info.Entities.Single(e => e.Id == entityId).Behaviours.Single(b => b.Id == behaviourId);

		// An entity-tagged listener with no BehaviourId now means "fan out to every behaviour on tagged
		// entities" — the BehaviourId is preserved as null rather than coerced to "" (which matched nothing).
		[Test]
		public void EntityTaggedListener_WithoutBehaviourId_HasNullBehaviourId()
		{
			var info = Transform(@"
Entities:
  player:
    Behaviours:
      fire:
        Type: on start trigger
        Listeners:
          - EntityTag: enemy
");

			var listener = (EntityTaggedListenerInfo)Behaviour(info, "player", "fire").Listeners.Single();
			Assert.IsNull(listener.BehaviourId);
		}

		[Test]
		public void EntityTaggedListener_WithBehaviourId_CarriesIt()
		{
			var info = Transform(@"
Entities:
  player:
    Behaviours:
      fire:
        Type: on start trigger
        Listeners:
          - EntityTag: enemy
            BehaviourId: explode
");

			var listener = (EntityTaggedListenerInfo)Behaviour(info, "player", "fire").Listeners.Single();
			Assert.AreEqual("explode", listener.BehaviourId);
		}

		// A typed-list property (`!string [...]`) no longer silently drops to empty — it parses the same as
		// an untyped list, so a collision trigger actually detects the tags it declares.
		[Test]
		public void TypedListTagsToDetect_AreParsed()
		{
			var info = Transform(@"
Entities:
  ball:
    Behaviours:
      hit:
        Type: collision enter trigger
        Properties:
          TagsToDetect: !string [ wall, paddle ]
");

			var trigger = (CollisionEnterTriggerInfo)Behaviour(info, "ball", "hit");
			CollectionAssert.AreEqual(new[] { "wall", "paddle" }, trigger.TagsToDetect);
		}

		// `when all` used to be parse-only (rejected at parse time); it is now runnable, so it parses cleanly.
		// The parse-time rejection mechanism still guards any future not-yet-runnable behaviour (empty today),
		// covered structurally by BehaviourRegistryParseOnlyTests.
		[Test]
		public void FormerlyParseOnlyBehaviour_NowParses()
		{
			Assert.DoesNotThrow(() => Transform(@"
Entities:
  e:
    Behaviours:
      gate:
        Type: when all
        Properties:
          TriggerIds: [ a, b ]
"));
		}
	}
}
