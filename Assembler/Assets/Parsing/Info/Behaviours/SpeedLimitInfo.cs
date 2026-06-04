using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SpeedLimitInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Velocity,
		ValueSource<float> Max)
		: BehaviourInfo(Id, Listeners)
	{
		public static SpeedLimitInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateOptionalValueSource<Vector3>(ctx, props.GetValueOrDefault("Velocity")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("Max")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new SpeedLimitInfo(Id,
				substitutedListeners,
				Velocity.SubstituteParameters(ctx),
				Max.SubstituteParameters(ctx));
	}
}
