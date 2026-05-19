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
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Sprite>(v, props.GetValueOrDefault("Sprite"), parameters: p),
				Transformer.CreateValueSource<Vector2>(v, props.GetValueOrDefault("Size"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new SpriteInfo(Id,
				substitutedListeners,
				Sprite.SubstituteParameters(parameters, allValues),
				Size.SubstituteParameters(parameters, allValues));
	}
}