using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record VelocityInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<Vector3> Velocity)
		: BehaviourInfo(Id, Listeners)
	{
		public static VelocityInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Velocity")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new VelocityInfo(Id,
				substitutedListeners,
				Velocity.SubstituteParameters(ctx));
	}
}