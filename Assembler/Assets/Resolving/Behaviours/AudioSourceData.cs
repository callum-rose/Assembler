using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public class AudioSourceData : BehaviourData
	{
		public IValueProvider<AudioClip> Clip { get; }
		public IValueProvider<bool> PlayOnStart { get; }
		public IValueProvider<bool> Loop { get; }

		public AudioSourceData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<AudioClip> clip,
			IValueProvider<bool> playOnStart,
			IValueProvider<bool> loop) : base(id, listeners) =>
			(Clip, PlayOnStart, Loop) = (clip, playOnStart, loop);
	}
}