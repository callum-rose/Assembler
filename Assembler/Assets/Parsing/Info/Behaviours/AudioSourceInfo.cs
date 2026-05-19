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
			Dictionary<string, AssemblerValue>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue>? p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<AudioClip>(v, props?.GetValueOrDefault("Clip"), parameters: p),
				Transformer.CreateValueSource<bool>(v, props?.GetValueOrDefault("PlayOnStart"), parameters: p),
				Transformer.CreateValueSource<bool>(v, props?.GetValueOrDefault("Loop"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new AudioSourceInfo(Id,
				substitutedListeners,
				Clip.SubstituteParameters(parameters, allValues),
				PlayOnStart.SubstituteParameters(parameters, allValues),
				Loop.SubstituteParameters(parameters, allValues));
	}
}