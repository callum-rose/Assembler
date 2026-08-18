using System;
using DG.Tweening;
using UnityEngine;

namespace Assembler.Shell.Theming
{
	/// <summary>
	/// The shell's timing block. Every animation in the app names one of these specs rather than carrying its
	/// own literal, so the whole app's pace is a single asset edit.
	/// </summary>
	[Serializable]
	public sealed class MotionSettings
	{
		[Tooltip("Screen-to-screen crossfade. A newspaper turns pages; it doesn't slide drawers.")]
		[SerializeField] private MotionSpec screenTransition = new(0.12f, Ease.InOutQuad);

		[Tooltip("A letterpress button's face travelling onto its plate, and back.")]
		[SerializeField] private MotionSpec buttonPress = new(0.08f, Ease.OutQuad);

		[Tooltip("The scrim behind an overlay fading in.")]
		[SerializeField] private MotionSpec overlayFade = new(0.18f, Ease.OutQuad);

		[Tooltip("A bottom sheet rising into place.")]
		[SerializeField] private MotionSpec sheetRise = new(0.24f, Ease.OutCubic);

		[Tooltip("The result slip landing.")]
		[SerializeField] private MotionSpec slipPop = new(0.26f, Ease.OutBack);

		[Tooltip("The verdict stamp's scale-slam onto the slip.")]
		[SerializeField] private MotionSpec verdictStamp = new(0.4f, Ease.OutBack);

		[Tooltip("The slip's score counting up, digits held in <mspace> while it runs.")]
		[SerializeField] private MotionSpec scoreOdometer = new(0.7f, Ease.OutCubic);

		[Tooltip("The launch overlay's progress bar advancing to a new stage.")]
		[SerializeField] private MotionSpec launchProgress = new(0.55f, Ease.OutQuad);

		public MotionSpec ScreenTransition => screenTransition;

		public MotionSpec ButtonPress => buttonPress;

		public MotionSpec OverlayFade => overlayFade;

		public MotionSpec SheetRise => sheetRise;

		public MotionSpec SlipPop => slipPop;

		public MotionSpec VerdictStamp => verdictStamp;

		public MotionSpec ScoreOdometer => scoreOdometer;

		public MotionSpec LaunchProgress => launchProgress;
	}
}
