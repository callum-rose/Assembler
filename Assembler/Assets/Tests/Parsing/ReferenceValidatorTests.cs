using Assembler.Deserialisation;
using Assembler.Parsing;
using NUnit.Framework;

namespace Tests.Parsing
{
	// Covers the transform-time reference-resolution pass (ReferenceValidator): a typo'd
	// entity/behaviour reference in a listener must fail fast with a legible ParsingException,
	// while references that are only knowable at build time must not be flagged (false-positive guards).
	public class ReferenceValidatorTests
	{
		private static void Transform(string yaml)
		{
			var gameDto = new GameFileParser().Parse(yaml);
			Transformer.Transform(gameDto);
		}

		private static ParsingException AssertThrows(string yaml) =>
			Assert.Throws<ParsingException>(() => Transform(yaml))!;

		// ---- Must throw -------------------------------------------------------------------------

		[Test]
		public void Listener_TargetsUnknownEntity_Throws()
		{
			var yaml = @"
Entities:
  source:
    Behaviours:
      trig:
        Type: interval trigger
        Properties: { Interval: 1.0 }
        Listeners:
          - { EntityId: nonexistent, BehaviourId: b }
";

			var ex = AssertThrows(yaml);
			StringAssert.Contains("nonexistent", ex.Message);
		}

		[Test]
		public void Listener_TargetsUnknownBehaviourOnKnownEntity_Throws()
		{
			var yaml = @"
Entities:
  target:
    Behaviours:
      real:
        Type: velocity
        Properties: { Velocity: !vec { X: 0, Y: 0 } }
  source:
    Behaviours:
      trig:
        Type: interval trigger
        Properties: { Interval: 1.0 }
        Listeners:
          - { EntityId: target, BehaviourId: ghost }
";

			var ex = AssertThrows(yaml);
			StringAssert.Contains("ghost", ex.Message);
			StringAssert.Contains("target", ex.Message);
		}

		[Test]
		public void Listener_TargetsGameOverWithNonEndBehaviour_Throws()
		{
			var yaml = @"
Entities:
  source:
    Behaviours:
      trig:
        Type: interval trigger
        Properties: { Interval: 1.0 }
        Listeners:
          - { EntityId: ""$gameover$"", BehaviourId: boom }
";

			var ex = AssertThrows(yaml);
			StringAssert.Contains("boom", ex.Message);
			StringAssert.Contains("end", ex.Message);
		}

		[Test]
		public void SetBehaviourEnabled_TargetsUnknownEntity_Throws()
		{
			var yaml = @"
Entities:
  source:
    Behaviours:
      freeze:
        Type: set behaviour enabled
        Properties:
          Enabled: false
          Targets:
            - { EntityId: ghostentity, BehaviourId: drift }
";

			var ex = AssertThrows(yaml);
			StringAssert.Contains("ghostentity", ex.Message);
		}

		// ---- Must NOT throw (false-positive guards) ---------------------------------------------

		[Test]
		public void Listener_TargetsPlacementInstance_DoesNotThrow()
		{
			var yaml = @"
Templates:
  brick:
    Behaviours:
      b:
        Type: velocity
        Properties: { Velocity: !vec { X: 0, Y: 0 } }
Placements:
  wall:
    Template: brick
    At:
      - !vec { X: 0, Y: 0, Z: 0 }
Entities:
  source:
    Behaviours:
      trig:
        Type: interval trigger
        Properties: { Interval: 1.0 }
        Listeners:
          - { EntityId: wall_0, BehaviourId: b }
";

			Assert.DoesNotThrow(() => Transform(yaml));
		}

		[Test]
		public void Listener_TargetsSpawnedEntity_DoesNotThrow()
		{
			var yaml = @"
Entities:
  source:
    Behaviours:
      trig:
        Type: interval trigger
        Properties: { Interval: 1.0 }
        Listeners:
          - { EntityId: ""$spawn$foo_0"", BehaviourId: b }
";

			Assert.DoesNotThrow(() => Transform(yaml));
		}

		[Test]
		public void Listener_TargetsGameOverEnd_DoesNotThrow()
		{
			var yaml = @"
Entities:
  source:
    Behaviours:
      trig:
        Type: interval trigger
        Properties: { Interval: 1.0 }
        Listeners:
          - { EntityId: ""$gameover$"", BehaviourId: end }
";

			Assert.DoesNotThrow(() => Transform(yaml));
		}

		[Test]
		public void Listener_TaggedTargetsThatMatchNoEntity_DoesNotThrow()
		{
			var yaml = @"
Entities:
  source:
    Behaviours:
      trig:
        Type: interval trigger
        Properties: { Interval: 1.0 }
        Listeners:
          - { EntityTag: nonexistent-tag }
          - { BehaviourTag: nonexistent-tag }
";

			Assert.DoesNotThrow(() => Transform(yaml));
		}

		[Test]
		public void Listener_TargetsDeclaredChild_DoesNotThrow()
		{
			var yaml = @"
Entities:
  parent:
    Behaviours:
      trig:
        Type: interval trigger
        Properties: { Interval: 1.0 }
        Listeners:
          - { EntityId: parent/kid, BehaviourId: k }
    Children:
      kid:
        Behaviours:
          k:
            Type: velocity
            Properties: { Velocity: !vec { X: 0, Y: 0 } }
";

			Assert.DoesNotThrow(() => Transform(yaml));
		}

		[Test]
		public void TemplateOnlyListener_StillParameterised_DoesNotThrow()
		{
			// A template that is never instantiated keeps its listener's EntityId as a pending
			// !parameter — not a literal — so the pass must leave it (and the template) alone.
			var yaml = @"
Templates:
  proj:
    Behaviours:
      b:
        Type: velocity
        Properties: { Velocity: !vec { X: 0, Y: 0 } }
        Listeners:
          - { EntityId: !parameter target_id, BehaviourId: b }
Entities:
  source:
    Behaviours:
      v:
        Type: velocity
        Properties: { Velocity: !vec { X: 1, Y: 0 } }
";

			Assert.DoesNotThrow(() => Transform(yaml));
		}
	}
}
