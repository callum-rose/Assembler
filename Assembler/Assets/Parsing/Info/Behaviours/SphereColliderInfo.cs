using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SphereColliderInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> Radius,
		ValueSource<bool> IsTrigger,
		ValueSource<float> Bounciness,
		ValueSource<float> DynamicFriction,
		ValueSource<float> StaticFriction) : BehaviourInfo(Id, Listeners)
	{
		public static SphereColliderInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Radius")),
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("IsTrigger")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Bounciness")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("DynamicFriction")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("StaticFriction")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new SphereColliderInfo(Id,
				substitutedListeners,
				Radius.SubstituteParameters(ctx),
				IsTrigger.SubstituteParameters(ctx),
				Bounciness.SubstituteParameters(ctx),
				DynamicFriction.SubstituteParameters(ctx),
				StaticFriction.SubstituteParameters(ctx));
	}
}
