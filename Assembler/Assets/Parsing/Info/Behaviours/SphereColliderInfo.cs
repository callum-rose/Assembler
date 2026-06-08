using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SphereColliderInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> Radius,
		ValueSource<bool> IsTrigger) : BehaviourInfo(Id, Listeners)
	{
		public static SphereColliderInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("Radius")),
				Transformer.CreateValueSource<bool>(ctx, props.GetValueOrDefault("IsTrigger")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new SphereColliderInfo(Id,
				substitutedListeners,
				Radius.SubstituteParameters(ctx),
				IsTrigger.SubstituteParameters(ctx));
	}
}
