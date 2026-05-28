using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CapsuleColliderInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> Radius,
		ValueSource<float> Height,
		ValueSource<int> Direction,
		ValueSource<bool> IsTrigger) : BehaviourInfo(Id, Listeners)
	{
		public static CapsuleColliderInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("Radius")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("Height")),
				Transformer.CreateValueSource<int>(ctx, props.GetValueOrDefault("Direction")),
				Transformer.CreateValueSource<bool>(ctx, props.GetValueOrDefault("IsTrigger")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new CapsuleColliderInfo(Id,
				substitutedListeners,
				Radius.SubstituteParameters(ctx),
				Height.SubstituteParameters(ctx),
				Direction.SubstituteParameters(ctx),
				IsTrigger.SubstituteParameters(ctx));
	}
}
