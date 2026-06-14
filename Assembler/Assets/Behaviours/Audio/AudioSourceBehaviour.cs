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
	public class AudioSourceBehaviour : GameBehaviour<AudioSourceData>, IAmExecutable
	{
		private AudioSource _audioSource;

		protected override void OnInitialise(AudioSourceData data)
		{
			// Create the source once and reuse it across pooled lives (OnInitialise re-runs on every reuse, so a
			// guard keeps the single AudioSource rather than stacking a second). A guard rather than Awake because
			// Awake does not run in edit mode (the sandbox validator / EditMode tests build via OnInitialise).
			if (_audioSource == null)
			{
				_audioSource = gameObject.AddComponent<AudioSource>();
			}

			_audioSource.clip = data.Clip.Get();
			_audioSource.loop = data.Loop.Get();

			if (data.PlayOnStart.Get())
			{
				_audioSource.Play();
			}
		}

		public void Execute(TriggerContext ctx)
		{
			_audioSource.Play();
		}
	}
}
