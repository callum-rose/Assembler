using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record AddImpulseInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<Vector3> Impulse)
		: BehaviourInfo(Id, Listeners)
	{
		public static AddImpulseInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Impulse")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new AddImpulseInfo(Id,
				substitutedListeners,
				Impulse.SubstituteParameters(ctx));
	}
}
