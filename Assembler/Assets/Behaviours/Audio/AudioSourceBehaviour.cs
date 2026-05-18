using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Audio
{
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
