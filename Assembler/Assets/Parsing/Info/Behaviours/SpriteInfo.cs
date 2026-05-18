using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SpriteInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Sprite> Sprite,
		ValueSource<Vector2> Size)
		: BehaviourInfo(Id, Listeners)
	{
		public static SpriteInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<Sprite>(v, props?.GetValueOrDefault("Sprite"), parameters: p),
				Transformer.Wrap<Vector2>(v, props?.GetValueOrDefault("Size"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new SpriteInfo(Id,
				substitutedListeners,
				Sprite.Substitute(parameters, allValues),
				Size.Substitute(parameters, allValues));
	}
}