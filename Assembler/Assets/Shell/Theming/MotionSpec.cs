using System;
using DG.Tweening;
using UnityEngine;

namespace Assembler.Shell.Theming
{
	/// <summary>
	/// A duration and an easing curve — the two numbers every shell tween needs. Motion literals are banned in
	/// shell code; a tween reads its spec from <see cref="MotionSettings"/> so the app's timing can be retuned
	/// in one asset.
	/// </summary>
	[Serializable]
	public sealed class MotionSpec
	{
		[Tooltip("Seconds.")]
		[SerializeField] private float duration = 0.2f;

		[SerializeField] private Ease ease = Ease.OutQuad;

		public MotionSpec()
		{
		}

		public MotionSpec(float duration, Ease ease)
		{
			this.duration = duration;
			this.ease = ease;
		}

		public float Duration => duration;

		public Ease Ease => ease;
	}
}
