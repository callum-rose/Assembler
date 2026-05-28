using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record BoxColliderInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Size,
		ValueSource<bool> IsTrigger) : BehaviourInfo(Id, Listeners)
	{
		public static BoxColliderInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Size")),
				Transformer.CreateValueSource<bool>(ctx, props.GetValueOrDefault("IsTrigger")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new BoxColliderInfo(Id,
				substitutedListeners,
				Size.SubstituteParameters(ctx),
				IsTrigger.SubstituteParameters(ctx));
	}
}