using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CapsuleColliderInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> Radius,
		ValueSource<float> Height,
		ValueSource<int> Direction,
		ValueSource<bool> IsTrigger,
		ValueSource<float> Bounciness,
		ValueSource<float> DynamicFriction,
		ValueSource<float> StaticFriction) : BehaviourInfo(Id, Listeners)
	{
		public static CapsuleColliderInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Radius")),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Height")),
				ValueSourceFactory.CreateValueSource<int>(ctx, props.GetValueOrDefault("Direction")),
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("IsTrigger")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Bounciness")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("DynamicFriction")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("StaticFriction")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new CapsuleColliderInfo(Id,
				substitutedListeners,
				Radius.SubstituteParameters(ctx),
				Height.SubstituteParameters(ctx),
				Direction.SubstituteParameters(ctx),
				IsTrigger.SubstituteParameters(ctx),
				Bounciness.SubstituteParameters(ctx),
				DynamicFriction.SubstituteParameters(ctx),
				StaticFriction.SubstituteParameters(ctx));
	}
}
