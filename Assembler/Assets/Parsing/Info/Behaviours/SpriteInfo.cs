using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SpriteInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Sprite> Sprite,
		ValueSource<Vector3> Size)
		: BehaviourInfo(Id, Listeners)
	{
		public static SpriteInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<Sprite>(ctx, props.GetValueOrDefault("Sprite")),
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Size")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new SpriteInfo(Id,
				substitutedListeners,
				Sprite.SubstituteParameters(ctx),
				Size.SubstituteParameters(ctx));
	}
}
