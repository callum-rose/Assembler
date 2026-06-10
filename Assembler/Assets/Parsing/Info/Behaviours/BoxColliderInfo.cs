using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record BoxColliderInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Size,
		ValueSource<bool> IsTrigger,
		ValueSource<float> Bounciness,
		ValueSource<float> DynamicFriction,
		ValueSource<float> StaticFriction) : BehaviourInfo(Id, Listeners)
	{
		public static BoxColliderInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Size")),
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("IsTrigger")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Bounciness")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("DynamicFriction")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("StaticFriction")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new BoxColliderInfo(Id,
				substitutedListeners,
				Size.SubstituteParameters(ctx),
				IsTrigger.SubstituteParameters(ctx),
				Bounciness.SubstituteParameters(ctx),
				DynamicFriction.SubstituteParameters(ctx),
				StaticFriction.SubstituteParameters(ctx));
	}
}
