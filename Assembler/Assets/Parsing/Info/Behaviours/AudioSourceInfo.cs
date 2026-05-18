using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record AudioSourceInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<AudioClip> Clip,
		ValueSource<bool> PlayOnStart,
		ValueSource<bool> Loop)
		: BehaviourInfo(Id, Listeners)
	{
		public static AudioSourceInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<AudioClip>(v, props?.GetValueOrDefault("Clip"), parameters: p),
				Transformer.Wrap<bool>(v, props?.GetValueOrDefault("PlayOnStart"), parameters: p),
				Transformer.Wrap<bool>(v, props?.GetValueOrDefault("Loop"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new AudioSourceInfo(Id,
				substitutedListeners,
				Clip.Substitute(parameters, allValues),
				PlayOnStart.Substitute(parameters, allValues),
				Loop.Substitute(parameters, allValues));
	}
}