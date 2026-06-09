using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	/// <summary>One named steering force with a blend weight. The force is any Vector3-valued source (typically a
	/// <c>!expr</c> calling a SteeringMath helper such as Seek/Separate).</summary>
	public sealed record SteeringForceInfo(ValueSource<Vector3> Force, ValueSource<float> Weight)
	{
		public SteeringForceInfo SubstituteParameters(TransformContext ctx) =>
			this with { Force = Force.SubstituteParameters(ctx), Weight = Weight.SubstituteParameters(ctx) };
	}

	/// <summary>
	/// Aggregator that blends a weighted list of steering forces into a single desired velocity. Saves authors
	/// from hand-summing forces inside one giant <c>!expr</c>: list each force with a weight and the behaviour
	/// produces the weighted sum, clamped to <c>MaxSpeed</c>. Writes the result to <c>Output</c> (a <c>!var</c>
	/// velocity variable an integrator reads) or, when no output is bound, drives the entity directly.
	/// </summary>
	public record SteeringInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		IReadOnlyList<SteeringForceInfo> Forces,
		ValueSource<float> MaxSpeed,
		ValueSource<Vector3> Output) : BehaviourInfo(Id, Listeners)
	{
		public static SteeringInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ParseForces(ctx, props.GetValueOrDefault("Forces"), id),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("MaxSpeed"), float.PositiveInfinity),
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, props.GetValueOrDefault("Output")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new SteeringInfo(Id,
				substitutedListeners,
				Forces.Select(f => f.SubstituteParameters(ctx)).ToArray(),
				MaxSpeed.SubstituteParameters(ctx),
				Output.SubstituteParameters(ctx));

		private static IReadOnlyList<SteeringForceInfo> ParseForces(
			TransformContext ctx,
			AssemblerValue? raw,
			string id)
		{
			if (raw is null or NoValue)
			{
				return System.Array.Empty<SteeringForceInfo>();
			}

			if (raw is not ListValue list)
			{
				throw new ParsingException(
					$"steering '{id}': Forces must be a list of {{ Force, Weight }} entries.");
			}

			return list.Value.Select(item => item switch
			{
				DictValue d => new SteeringForceInfo(
					ValueSourceFactory.CreateValueSource<Vector3>(ctx, d.Value.GetValueOrDefault("Force")),
					ValueSourceFactory.CreateValueSource<float>(ctx, d.Value.GetValueOrDefault("Weight"), fallback: 1f)),
				_ => throw new ParsingException(
					$"steering '{id}': each Forces entry must be a {{ Force, Weight }} map.")
			}).ToArray();
		}
	}
}
