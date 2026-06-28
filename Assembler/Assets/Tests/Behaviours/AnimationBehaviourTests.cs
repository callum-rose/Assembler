using System.Linq;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;
using AnimationInfo = Assembler.Parsing.Info.Behaviours.AnimationInfo;

namespace Tests.Behaviours
{
	/// <summary>
	/// Parse → resolve coverage for the `animation` behaviour: a multi-step descriptor yields the expected
	/// ordered <see cref="AnimationStepInfo"/>s (including a wait and a join), the single-tween shorthand yields
	/// a one-step list, and the resolved <see cref="AnimationData"/> mirrors that structure. Live DOTween
	/// playback (which needs a ticking clock) is deliberately out of scope.
	/// </summary>
	public class AnimationBehaviourTests
	{
		// Every step in these descriptors is a constant, so resolution never touches a registry — a context of
		// nulls is enough to turn the value sources into ConstantValueProvider / NullValueProvider.
		private readonly static ResolutionContext ConstantContext =
			new(null!, null!, null!, null!, null!, null!, null!, null!);

		private static AnimationInfo ParseAnimation(string yaml) =>
			(AnimationInfo)Transformer.Transform(new GameFileParser().Parse(yaml)).Entities[0].Behaviours[0];

		private static AnimationData Build(AnimationInfo info) =>
			new(info.Id,
				info.Steps.Select(s => s.Resolve(ConstantContext)).ToArray(),
				info.Loops.Resolve(ConstantContext),
				info.LoopType.Resolve(ConstantContext));

		private const string SequenceYaml = @"
Entities:
  e:
    Behaviours:
      flourish:
        Type: animation
        Properties:
          Loops: 2
          LoopType: yoyo
          Steps:
            - { Animate: scale,  Start: !vec { X: 0, Y: 0, Z: 0 }, End: !vec { X: 1, Y: 1, Z: 1 }, Duration: 0.5, Easing: OutBack }
            - { Animate: move,   End: !vec { X: 0, Y: 2, Z: 0 }, Duration: 1.5, Easing: InOutSine }
            - { Animate: rotate, Mode: join, End: !vec { X: 0, Y: 0, Z: 360 }, Duration: 1.5 }
            - { Animate: wait,   Duration: 0.3 }
";

		[Test]
		public void SequenceParsesEachStepTargetAndMode()
		{
			var info = ParseAnimation(SequenceYaml);

			CollectionAssert.AreEqual(
				new[] { AnimationTarget.Scale, AnimationTarget.Move, AnimationTarget.Rotate, AnimationTarget.Wait },
				info.Steps.Select(s => ((ConstantSource<AnimationTarget>)s.Animate).Value).ToArray());

			CollectionAssert.AreEqual(
				new[] { SequenceMode.Append, SequenceMode.Append, SequenceMode.Join, SequenceMode.Append },
				info.Steps.Select(s => ((ConstantSource<SequenceMode>)s.Mode).Value).ToArray());
		}

		[Test]
		public void ResolvedDataMirrorsTheParsedSteps()
		{
			var data = Build(ParseAnimation(SequenceYaml));

			Assert.AreEqual(4, data.Steps.Count);

			CollectionAssert.AreEqual(
				new[] { AnimationTarget.Scale, AnimationTarget.Move, AnimationTarget.Rotate, AnimationTarget.Wait },
				data.Steps.Select(s => s.Target.Get(TriggerContext.Empty)).ToArray());

			CollectionAssert.AreEqual(
				new[] { SequenceMode.Append, SequenceMode.Append, SequenceMode.Join, SequenceMode.Append },
				data.Steps.Select(s => s.Mode.Get(TriggerContext.Empty)).ToArray());

			// The scale step set Start explicitly; the rotate step omitted it (chains from the live value).
			Assert.IsNotInstanceOf<NullValueProvider<Vector3>>(data.Steps[0].Start);
			Assert.IsInstanceOf<NullValueProvider<Vector3>>(data.Steps[2].Start);

			Assert.AreEqual(Vector3.one, data.Steps[0].End.Get(TriggerContext.Empty));
			Assert.AreEqual(Easing.OutBack, data.Steps[0].Easing.Get(TriggerContext.Empty));
			Assert.AreEqual(0.3f, data.Steps[3].Duration.Get(TriggerContext.Empty));

			Assert.AreEqual(2, data.Loops.Get(TriggerContext.Empty));
			Assert.AreEqual(SequenceLoopType.Yoyo, data.LoopType.Get(TriggerContext.Empty));
		}

		[Test]
		public void ShorthandYieldsASingleAppendStep()
		{
			var info = ParseAnimation(@"
Entities:
  e:
    Behaviours:
      drift:
        Type: animation
        Properties: { Animate: move, End: !vec { X: 0, Y: 2, Z: 0 }, Duration: 1.5, Easing: InOutSine }
");

			Assert.AreEqual(1, info.Steps.Count);
			Assert.AreEqual(AnimationTarget.Move, ((ConstantSource<AnimationTarget>)info.Steps[0].Animate).Value);
			Assert.AreEqual(SequenceMode.Append, ((ConstantSource<SequenceMode>)info.Steps[0].Mode).Value);

			// Default loop settings: plays once, restart.
			var data = Build(info);
			Assert.AreEqual(1, data.Loops.Get(TriggerContext.Empty));
			Assert.AreEqual(SequenceLoopType.Restart, data.LoopType.Get(TriggerContext.Empty));
		}

		[Test]
		public void MissingStepsAndShorthandThrows()
		{
			Assert.Throws<ParsingException>(() => ParseAnimation(@"
Entities:
  e:
    Behaviours:
      empty:
        Type: animation
        Properties: { Loops: 1 }
"));
		}
	}
}
