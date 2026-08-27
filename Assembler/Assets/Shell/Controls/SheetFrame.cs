using System;
using Assembler.Shell.Motion;
using Assembler.Shell.Theming;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assembler.Shell.Controls
{
	/// <summary>
	/// The chrome an overlay wears: a scrim over everything beneath it, a surface rising from the bottom edge,
	/// a grab bar, and a slot the overlay's own content is parented into.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The frame animates and reports; it does not decide. A tap on the scrim raises <see cref="Dismissed"/>
	/// rather than closing anything, because whether an overlay may be dismissed by tapping outside it is the
	/// overlay's business — a pause sheet says yes, a launch overlay mid-download says no. Deciding lands with
	/// the overlay host (UIPLAN 3.6).
	/// </para>
	/// <para>
	/// Both tweens run unscaled through <see cref="TweenExtensions.SetShellDefaults{T}"/>, which is what lets a
	/// sheet rise over a game frozen at <c>timeScale = 0</c> (UIPLAN 8.3, 10.3).
	/// </para>
	/// </remarks>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(RectTransform))]
	[AddComponentMenu("Assembler/Shell/Sheet Frame")]
	public sealed class SheetFrame : MonoBehaviour, IPointerClickHandler
	{
		[Header("Parts")]
		[Tooltip("The dark ground over everything beneath the sheet. Fades in as the sheet rises.")]
		[SerializeField] private CanvasGroup scrim = null!;

		[Tooltip("The scrim's hit target — what a tap outside the sheet lands on.")]
		[SerializeField] private HitTarget scrimHit = null!;

		[Tooltip("The surface that rises. Its resting anchored position is where it comes to rest.")]
		[SerializeField] private RectTransform sheet = null!;

		[Tooltip("Where an overlay parents its own content.")]
		[SerializeField] private RectTransform content = null!;

		[Header("Motion")]
		[Tooltip("How far below its resting place the sheet starts, in canvas units.")]
		[Min(0f)]
		[SerializeField] private float riseDistance = 18f;

		private Tween? _scrimTween;
		private Tween? _sheetTween;
		private Vector2 _restPosition;
		private bool _capturedRest;

		/// <summary>Raised when the scrim is tapped. The frame does not act on it.</summary>
		public event Action Dismissed = delegate { };

		/// <summary>Where an overlay parents its own content.</summary>
		public RectTransform Content => content;

		private void Awake()
		{
			CaptureRest();
		}

		private void OnDisable()
		{
			// The kill-on-disable link has already stopped these; dropping the handles stops a re-show from
			// reading a tween that is on its way out.
			_scrimTween = null;
			_sheetTween = null;
		}

		/// <inheritdoc/>
		public void OnPointerClick(PointerEventData eventData)
		{
			if (eventData.button != PointerEventData.InputButton.Left)
			{
				return;
			}

			// Only a press that landed on the scrim's own target counts. The sheet carries a target of its own,
			// so a tap on empty sheet is absorbed there rather than falling through and reading as a dismiss.
			if (scrimHit != null && eventData.pointerPressRaycast.gameObject == scrimHit.gameObject)
			{
				Dismissed.Invoke();
			}
		}

		/// <summary>Fades the scrim in and rises the sheet. Finishes when both tweens are done.</summary>
		public Awaitable Show()
		{
			CaptureRest();

			var motion = Theme.Current.Motion;
			var fade = motion.OverlayFade;
			var rise = motion.SheetRise;

			_scrimTween?.Kill();
			_sheetTween?.Kill();

			if (scrim != null)
			{
				scrim.alpha = 0f;
				_scrimTween = scrim.TweenAlpha(1f, fade.Duration).SetMotion(fade).SetShellDefaults(gameObject);
			}

			if (sheet == null)
			{
				return AwaitableOf(_scrimTween);
			}

			sheet.anchoredPosition = _restPosition - new Vector2(0f, riseDistance);
			_sheetTween = sheet
				.TweenAnchoredPosition(_restPosition, rise.Duration)
				.SetMotion(rise)
				.SetShellDefaults(gameObject);

			// The sheet outlasts the scrim, so awaiting it awaits the whole entrance.
			return AwaitableOf(_sheetTween);
		}

		/// <summary>Sinks the sheet and fades the scrim out. Finishes when both tweens are done.</summary>
		public Awaitable Dismiss()
		{
			CaptureRest();

			var motion = Theme.Current.Motion;
			var fade = motion.OverlayFade;
			var rise = motion.SheetRise;

			_scrimTween?.Kill();
			_sheetTween?.Kill();

			if (scrim != null)
			{
				_scrimTween = scrim.TweenAlpha(0f, fade.Duration).SetMotion(fade).SetShellDefaults(gameObject);
			}

			if (sheet == null)
			{
				return AwaitableOf(_scrimTween);
			}

			_sheetTween = sheet
				.TweenAnchoredPosition(_restPosition - new Vector2(0f, riseDistance), rise.Duration)
				.SetMotion(rise)
				.SetShellDefaults(gameObject);

			return AwaitableOf(_sheetTween);
		}

		// The resting place is authored, not computed — so it is read once, before the first Show moves the
		// sheet off it and makes the current position a lie.
		private void CaptureRest()
		{
			if (_capturedRest || sheet == null)
			{
				return;
			}

			_restPosition = sheet.anchoredPosition;
			_capturedRest = true;
		}

		private static Awaitable AwaitableOf(Tween? tween)
		{
			if (tween is not null)
			{
				return tween.ToAwaitable();
			}

			var completed = new AwaitableCompletionSource();
			completed.SetResult();

			return completed.Awaitable;
		}
	}
}
