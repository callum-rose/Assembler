using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record MeshColliderInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<bool> Convex,
		ValueSource<bool> IsTrigger,
		ValueSource<float> Bounciness,
		ValueSource<float> DynamicFriction,
		ValueSource<float> StaticFriction) : BehaviourInfo(Id, Listeners)
	{
		public static MeshColliderInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("Convex")),
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("IsTrigger")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Bounciness")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("DynamicFriction")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("StaticFriction")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new MeshColliderInfo(Id,
				substitutedListeners,
				Convex.SubstituteParameters(ctx),
				IsTrigger.SubstituteParameters(ctx),
				Bounciness.SubstituteParameters(ctx),
				DynamicFriction.SubstituteParameters(ctx),
				StaticFriction.SubstituteParameters(ctx));
	}
}
