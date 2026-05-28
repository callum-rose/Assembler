using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record MeshColliderInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<bool> Convex,
		ValueSource<bool> IsTrigger) : BehaviourInfo(Id, Listeners)
	{
		public static MeshColliderInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<bool>(ctx, props.GetValueOrDefault("Convex")),
				Transformer.CreateValueSource<bool>(ctx, props.GetValueOrDefault("IsTrigger")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new MeshColliderInfo(Id,
				substitutedListeners,
				Convex.SubstituteParameters(ctx),
				IsTrigger.SubstituteParameters(ctx));
	}
}
