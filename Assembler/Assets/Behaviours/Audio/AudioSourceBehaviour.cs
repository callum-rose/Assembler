using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Audio
{
	/// <summary>Plays an audio clip when Executed (or on start, if configured).</summary>
	/// <remarks>
	/// Properties:
	///   Clip: Asset reference to the audio clip to play.
	///   PlayOnStart: When true the clip plays automatically when the entity is created.
	///   Loop: When true the clip loops once started.
	/// </remarks>
	public class AudioSourceBehaviour : GameBehaviour<AudioSourceData>
	{
		private AudioSource _audioSource;

		protected override void OnInitialise(AudioSourceData data)
		{
			_audioSource = gameObject.AddComponent<AudioSource>();
			_audioSource.clip = data.Clip.Value;
			_audioSource.loop = data.Loop.Value;

			if (data.PlayOnStart.Value)
			{
				_audioSource.Play();
			}
		}

		public override void Execute()
		{
			_audioSource.Play();
		}
	}
}
