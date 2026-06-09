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
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<AudioClip>(ctx, props.GetValueOrDefault("Clip")),
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("PlayOnStart")),
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("Loop")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new AudioSourceInfo(Id,
				substitutedListeners,
				Clip.SubstituteParameters(ctx),
				PlayOnStart.SubstituteParameters(ctx),
				Loop.SubstituteParameters(ctx));
	}
}
