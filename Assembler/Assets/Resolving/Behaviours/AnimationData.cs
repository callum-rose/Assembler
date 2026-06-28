using System.Collections.Generic;
using Assembler.Parsing.Info.Behaviours;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	/// <summary>One resolved tween step: live providers for the value-bearing fields plus the fixed
	/// <see cref="Target"/>/<see cref="Mode"/> structure. <see cref="Start"/> and <see cref="At"/> are
	/// <see cref="NullValueProvider{T}"/> when omitted (chain from the live value / append after the previous step).</summary>
	public readonly struct AnimationStep
	{
		public AnimationTarget Target { get; }
		public SequenceMode Mode { get; }
		public IValueProvider<Vector3> Start { get; }
		public IValueProvider<Vector3> End { get; }
		public IValueProvider<float> Duration { get; }
		public IValueProvider<Easing> Easing { get; }
		public IValueProvider<float> At { get; }

		public AnimationStep(
			AnimationTarget target,
			SequenceMode mode,
			IValueProvider<Vector3> start,
			IValueProvider<Vector3> end,
			IValueProvider<float> duration,
			IValueProvider<Easing> easing,
			IValueProvider<float> at)
		{
			Target = target;
			Mode = mode;
			Start = start;
			End = end;
			Duration = duration;
			Easing = easing;
			At = at;
		}
	}

	public sealed class AnimationData : BehaviourData
	{
		public IReadOnlyList<AnimationStep> Steps { get; }
		public IValueProvider<int> Loops { get; }
		public IValueProvider<SequenceLoopType> LoopType { get; }

		public AnimationData(
			string id,
			IReadOnlyList<AnimationStep> steps,
			IValueProvider<int> loops,
			IValueProvider<SequenceLoopType> loopType) : base(id)
		{
			Steps = steps;
			Loops = loops;
			LoopType = loopType;
		}
	}

	public static class AnimationStepResolver
	{
		/// <summary>Resolves one parsed step into its runtime form, turning each value source into a live provider.</summary>
		public static AnimationStep Resolve(this AnimationStepInfo step, ResolutionContext ctx) =>
			new(step.Animate,
				step.Mode,
				step.Start.Resolve(ctx),
				step.End.Resolve(ctx),
				step.Duration.Resolve(ctx),
				step.Easing.Resolve(ctx),
				step.At.Resolve(ctx));
	}
}
