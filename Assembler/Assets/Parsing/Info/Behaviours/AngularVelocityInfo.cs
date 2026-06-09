using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record AngularVelocityInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<Vector3> AngularVelocity)
		: BehaviourInfo(Id, Listeners)
	{
		public static AngularVelocityInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("AngularVelocity")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new AngularVelocityInfo(Id,
				substitutedListeners,
				AngularVelocity.SubstituteParameters(ctx));
	}
}
