using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record AddForceInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<Vector3> Force)
		: BehaviourInfo(Id, Listeners)
	{
		public static AddForceInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Force")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new AddForceInfo(Id,
				substitutedListeners,
				Force.SubstituteParameters(ctx));
	}
}
