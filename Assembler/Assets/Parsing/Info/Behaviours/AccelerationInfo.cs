using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record AccelerationInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<Vector3> Acceleration)
		: BehaviourInfo(Id, Listeners)
	{
		public static AccelerationInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Acceleration")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new AccelerationInfo(Id,
				substitutedListeners,
				Acceleration.SubstituteParameters(ctx));
	}
}
