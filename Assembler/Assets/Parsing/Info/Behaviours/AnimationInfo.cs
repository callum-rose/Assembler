using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	/// <summary>One tween step of an <c>animation</c> sequence. <see cref="Animate"/> and <see cref="Mode"/> are
	/// fixed structural enums parsed at transform time; the value-bearing fields are <c>!var</c>/<c>!expr</c>-capable
	/// and resolved each time the sequence is (re)built on Execute. A <see cref="AnimationTarget.Wait"/> step is a
	/// pure delay — only <see cref="Duration"/> is read; <see cref="Mode"/>, <see cref="End"/>, <see cref="Start"/>,
	/// <see cref="At"/> and <see cref="Easing"/> are ignored.</summary>
	public sealed record AnimationStepInfo(
		AnimationTarget Animate,
		SequenceMode Mode,
		ValueSource<Vector3> Start,
		ValueSource<Vector3> End,
		ValueSource<float> Duration,
		ValueSource<Easing> Easing,
		ValueSource<float> At)
	{
		public AnimationStepInfo SubstituteParameters(TransformContext ctx) =>
			this with
			{
				Start = Start.SubstituteParameters(ctx),
				End = End.SubstituteParameters(ctx),
				Duration = Duration.SubstituteParameters(ctx),
				Easing = Easing.SubstituteParameters(ctx),
				At = At.SubstituteParameters(ctx)
			};
	}

	/// <summary>
	/// Compiles an ordered list of tween steps — sequential <em>and</em> parallel — into a single DOTween
	/// <c>Sequence</c>. Replaces the former move/scale/rotate animation behaviours: a one-tween animation is
	/// expressed with the top-level shorthand (<c>Animate</c>/<c>Start</c>/<c>End</c>/<c>Duration</c>/<c>Easing</c>,
	/// no <c>Steps</c>), while a richer sequence lists each step under <c>Steps</c> with a <c>Mode</c> placing it
	/// relative to the previous step. Listeners fire once, when the whole sequence completes.
	/// </summary>
	public record AnimationInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		IReadOnlyList<AnimationStepInfo> Steps,
		ValueSource<int> Loops,
		ValueSource<SequenceLoopType> LoopType) : BehaviourInfo(Id, Listeners)
	{
		public static AnimationInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ParseSteps(ctx, props, id),
				ValueSourceFactory.CreateValueSource<int>(ctx, props.GetValueOrDefault("Loops"), fallback: 1),
				ValueSourceFactory.CreateEnumSource(ctx, props.GetValueOrDefault("LoopType"), SequenceLoopType.Restart));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new AnimationInfo(Id,
				substitutedListeners,
				Steps.Select(s => s.SubstituteParameters(ctx)).ToArray(),
				Loops.SubstituteParameters(ctx),
				LoopType.SubstituteParameters(ctx));

		// A descriptor gives either a `Steps:` list (full sequence) or a single-tween shorthand (top-level
		// `Animate:` etc.). Anything else is an authoring error worth surfacing at transform time.
		private static IReadOnlyList<AnimationStepInfo> ParseSteps(
			TransformContext ctx,
			IReadOnlyDictionary<string, AssemblerValue> props,
			string id)
		{
			var stepsRaw = props.GetValueOrDefault("Steps");

			if (stepsRaw is not null and not NoValue)
			{
				if (stepsRaw is not ListValue list)
				{
					throw new ParsingException(
						$"animation '{id}': Steps must be a list of tween-step maps.");
				}

				return list.Items.Select(item => item switch
				{
					DictValue d => ParseStep(ctx, d.Value, id),
					_ => throw new ParsingException(
						$"animation '{id}': each Steps entry must be a {{ Animate, … }} map.")
				}).ToArray();
			}

			if (props.GetValueOrDefault("Animate") is not (null or NoValue))
			{
				return new[] { ParseStep(ctx, props, id) };
			}

			throw new ParsingException(
				$"animation '{id}': needs a Steps list, or a single-tween Animate (move/rotate/scale/wait).");
		}

		private static AnimationStepInfo ParseStep(
			TransformContext ctx,
			IReadOnlyDictionary<string, AssemblerValue> dict,
			string id)
		{
			var animate = ParseTarget(dict.GetValueOrDefault("Animate"), id);
			var mode = ParseMode(dict.GetValueOrDefault("Mode"), id);

			if (animate != AnimationTarget.Wait && dict.GetValueOrDefault("End") is null or NoValue)
			{
				throw new ParsingException($"animation '{id}': a '{animate}' step needs an End value.");
			}

			if (dict.GetValueOrDefault("Duration") is null or NoValue)
			{
				throw new ParsingException($"animation '{id}': each step needs a Duration.");
			}

			return new AnimationStepInfo(
				animate,
				mode,
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, dict.GetValueOrDefault("Start")),
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, dict.GetValueOrDefault("End")),
				ValueSourceFactory.CreateValueSource<float>(ctx, dict.GetValueOrDefault("Duration")),
				ValueSourceFactory.CreateEnumSource(ctx, dict.GetValueOrDefault("Easing"), Easing.InOutSine),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, dict.GetValueOrDefault("At")));
		}

		private static AnimationTarget ParseTarget(AssemblerValue? raw, string id) =>
			raw switch
			{
				StringValue s => BehaviourEnums.Parse<AnimationTarget>(s.Value),
				null or NoValue => throw new ParsingException(
					$"animation '{id}': each step needs an Animate value (move, rotate, scale, wait)."),
				_ => throw new ParsingException(
					$"animation '{id}': Animate must be a literal (move, rotate, scale, wait), not a binding.")
			};

		private static SequenceMode ParseMode(AssemblerValue? raw, string id) =>
			raw switch
			{
				StringValue s => BehaviourEnums.Parse<SequenceMode>(s.Value),
				null or NoValue => SequenceMode.Append,
				_ => throw new ParsingException(
					$"animation '{id}': Mode must be a literal (append, join, insert), not a binding.")
			};
	}
}
