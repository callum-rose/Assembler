using System;
using Assembler.Shell.Motion;
using Assembler.Shell.Theming;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assembler.Shell.Controls
{
	/// <summary>
	/// The shell's button: a face sitting on a plate that shows as a hard ledge below and to the right of it, and
	/// a press that travels the face onto the plate until the ledge is gone. Type pressed into paper, then
	/// released.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>It is a <see cref="Selectable"/>, not a <see cref="Button"/>.</b> Button's own transitions
	/// (colour tint, sprite swap, animator) are all the wrong shape for a press that moves geometry, and its
	/// <c>onClick</c> comes bundled with them. Subclassing <see cref="Selectable"/> keeps what is worth having —
	/// interactable gating, the EventSystem's press bookkeeping, and slide-off-cancel for free, since a click is
	/// only raised when the pointer goes up over the same object it went down on.
	/// </para>
	/// <para>
	/// <b>Structure.</b> <c>Plate</c> is the ledge, inset from the top-left; <c>Face</c> is the ledge's mirror,
	/// inset from the bottom-right, and is what moves; <c>Fill</c> sits inside the face inset by the outline
	/// width, so that painting face and fill different roles turns the button into an outlined one with no change
	/// of structure; <c>HitTarget</c> is the stationary rect the pointer actually hits (UIPLAN 7.4). A button
	/// with no plate has no ledge to consume, so it sinks — that is the icon variant, whose face carries a
	/// <c>Glyph</c> from the UI atlas in place of a label.
	/// </para>
	/// <para>
	/// <b>Runtime only, deliberately — no <c>[ExecuteAlways]</c>.</b> The inset the ledge cuts is read from the
	/// theme and written onto the face and plate rects at enable; doing that in edit mode would run DOTween and
	/// Selectable's registration outside play mode for no gain. The prefabs are authored with the same numbers,
	/// so the scene still reads correctly without entering play mode.
	/// </para>
	/// </remarks>
	[RequireComponent(typeof(RectTransform))]
	[RequireComponent(typeof(CanvasGroup))]
	[AddComponentMenu("Assembler/Shell/Letterpress Button")]
	public sealed class LetterpressButton : Selectable, IPointerClickHandler, ISubmitHandler
	{
		[Header("Parts")]
		[Tooltip("The ledge. Leave empty for a button with no ledge — the press sinks instead of travelling.")]
		[SerializeField] private RectTransform? plate;

		[Tooltip("The face. This is the part that moves.")]
		[SerializeField] private RectTransform face = null!;

		[Tooltip("The face's inner fill. Paint it a different role from the face to get an outlined button.")]
		[SerializeField] private RectTransform? fill;

		[Tooltip("The label on the face. Empty for an icon button whose glyph is a graphic.")]
		[SerializeField] private TMP_Text? label;

		[Tooltip("The glyph on the face. Empty for a button that carries a label instead.")]
		[SerializeField] private Image? icon;

		[Tooltip("The stationary rect the pointer hits. Never animated, never smaller than the theme's minimum.")]
		[SerializeField] private HitTarget hitTarget = null!;

		[Header("Behaviour")]
		[Tooltip("How far a ledgeless button shrinks when pressed. Ignored when a plate is assigned.")]
		[Range(0.5f, 1f)]
		[SerializeField] private float sinkScale = 0.9f;

		[Tooltip("What the whole button fades to when it stops being interactable.")]
		[Range(0f, 1f)]
		[SerializeField] private float disabledAlpha = 0.4f;

		[SerializeField] private ClickEvent onClick = new();

		private CanvasGroup? _canvasGroup;
		private Tween? _pressTween;
		private Tween? _fadeTween;
		private bool _isPressed;

		/// <summary>Raised when the button is clicked or submitted, and only while it is interactable.</summary>
		public ClickEvent OnClick => onClick;

		/// <summary>The label's text. Empty on an icon button, which has no label.</summary>
		public string Text
		{
			get => label == null ? string.Empty : label.text;
			set
			{
				if (label != null)
				{
					label.SetText(value);
				}
			}
		}

		/// <summary>The glyph on an icon button's face. Null on a button that carries a label instead.</summary>
		public Sprite? Glyph
		{
			get => icon == null ? null : icon.sprite;
			set
			{
				if (icon != null)
				{
					icon.sprite = value;
				}
			}
		}

		/// <summary>The stationary rect the pointer hits — for a caller that needs to size it explicitly.</summary>
		public HitTarget HitArea => hitTarget;

		protected override void OnEnable()
		{
			base.OnEnable();

			if (!Application.isPlaying)
			{
				return;
			}

			Theme.Changed += ApplyLedge;
			ApplyLedge();
			SetPressed(false, instant: true);
			SetFaded(!IsInteractable(), instant: true);
		}

		protected override void OnDisable()
		{
			if (Application.isPlaying)
			{
				Theme.Changed -= ApplyLedge;
			}

			// The link set by SetShellDefaults kills these anyway; clearing the handles keeps a re-enable from
			// reading a tween that is on its way out.
			_pressTween = null;
			_fadeTween = null;
			_isPressed = false;

			base.OnDisable();
		}

#if UNITY_EDITOR
		protected override void Reset()
		{
			base.Reset();
			transition = Transition.None;
			navigation = new Navigation { mode = Navigation.Mode.None };
		}
#endif

		/// <inheritdoc/>
		public void OnPointerClick(PointerEventData eventData)
		{
			if (eventData.button != PointerEventData.InputButton.Left)
			{
				return;
			}

			Press();
		}

		/// <inheritdoc/>
		public void OnSubmit(BaseEventData eventData)
		{
			if (!IsActive() || !IsInteractable())
			{
				return;
			}

			// A submit has no pointer down/up pair, so the press has to be acted out in one go — otherwise the
			// key registers with no sign that it did.
			FlashPress();
			Press();
		}

		public override void OnPointerDown(PointerEventData eventData)
		{
			base.OnPointerDown(eventData);

			if (eventData.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable())
			{
				return;
			}

			SetPressed(true, instant: false);
		}

		// Sent to whichever object took the pointer down, wherever the pointer has since travelled — so the face
		// comes back up on a slide-off too, while OnPointerClick correctly does not fire.
		public override void OnPointerUp(PointerEventData eventData)
		{
			base.OnPointerUp(eventData);

			if (eventData.button != PointerEventData.InputButton.Left)
			{
				return;
			}

			SetPressed(false, instant: false);
		}

		protected override void DoStateTransition(SelectionState state, bool instant)
		{
			// Transition is None, so the base call only bookkeeps. The visual states are ours: pressed is driven
			// by the pointer handlers, disabled by the fade below.
			base.DoStateTransition(state, instant);

			if (!Application.isPlaying)
			{
				return;
			}

			bool disabled = state == SelectionState.Disabled;
			SetFaded(disabled, instant);

			if (disabled && _isPressed)
			{
				SetPressed(false, instant: true);
			}
		}

		// The ledge is a theme measurement, so the rects that express it are written from the theme rather than
		// trusted to whatever the prefab was last saved with.
		private void ApplyLedge()
		{
			if (face == null)
			{
				return;
			}

			var layout = Theme.Current.Layout;

			if (plate != null)
			{
				float ledge = layout.LetterpressLedge;

				// Vector2 is forced here by the RectTransform offset API.
				Stretch(face, new Vector2(0f, ledge), new Vector2(-ledge, 0f));
				Stretch(plate, new Vector2(ledge, 0f), new Vector2(0f, -ledge));
			}

			if (fill != null)
			{
				float outline = layout.OutlineWidth;
				Stretch(fill, new Vector2(outline, outline), new Vector2(-outline, -outline));
			}

			if (!_isPressed)
			{
				ApplyRestTransform();
			}
		}

		private void Press()
		{
			if (!IsActive() || !IsInteractable())
			{
				return;
			}

			onClick.Invoke();
		}

		// Down and straight back up again, as one tween, so that the second half cannot kill the first before it
		// has been seen — which is exactly what two back-to-back SetPressed calls would do.
		private void FlashPress()
		{
			if (face == null)
			{
				return;
			}

			_pressTween?.Kill();
			_isPressed = true;

			var spec = Theme.Current.Motion.ButtonPress;
			var sequence = DOTween.Sequence();

			if (plate != null)
			{
				sequence
					.Append(face.TweenAnchoredPosition(PressedFacePosition, spec.Duration).SetMotion(spec))
					.Append(face.TweenAnchoredPosition(RestFacePosition, spec.Duration).SetMotion(spec));
			}
			else
			{
				sequence
					.Append(face.DOScale(new Vector3(sinkScale, sinkScale, 1f), spec.Duration).SetMotion(spec))
					.Append(face.DOScale(Vector3.one, spec.Duration).SetMotion(spec));
			}

			sequence.onKill += () => _isPressed = false;

			_pressTween = sequence.SetShellDefaults(gameObject);
		}

		private void SetPressed(bool pressed, bool instant)
		{
			if (face == null)
			{
				return;
			}

			_isPressed = pressed;
			_pressTween?.Kill();
			_pressTween = null;

			var spec = Theme.Current.Motion.ButtonPress;

			// With a plate the face travels the ledge and lands flush on it; without one there is nothing to
			// land on, so the face sinks in place instead.
			if (plate != null)
			{
				var target = pressed ? PressedFacePosition : RestFacePosition;

				if (instant)
				{
					face.anchoredPosition = target;
					return;
				}

				_pressTween = face
					.TweenAnchoredPosition(target, spec.Duration)
					.SetMotion(spec)
					.SetShellDefaults(gameObject);

				return;
			}

			float scale = pressed ? sinkScale : 1f;

			if (instant)
			{
				face.localScale = new Vector3(scale, scale, 1f);
				return;
			}

			_pressTween = face
				.DOScale(new Vector3(scale, scale, 1f), spec.Duration)
				.SetMotion(spec)
				.SetShellDefaults(gameObject);
		}

		private void SetFaded(bool faded, bool instant)
		{
			_canvasGroup = _canvasGroup != null ? _canvasGroup : GetComponent<CanvasGroup>();

			if (_canvasGroup == null)
			{
				return;
			}

			float target = faded ? disabledAlpha : 1f;
			_fadeTween?.Kill();
			_fadeTween = null;

			if (instant || !Application.isPlaying)
			{
				_canvasGroup.alpha = target;
				return;
			}

			var spec = Theme.Current.Motion.ButtonPress;

			_fadeTween = _canvasGroup
				.TweenAlpha(target, spec.Duration)
				.SetMotion(spec)
				.SetShellDefaults(gameObject);
		}

		private void ApplyRestTransform()
		{
			if (plate != null)
			{
				face.anchoredPosition = RestFacePosition;
				face.localScale = Vector3.one;
				return;
			}

			face.localScale = Vector3.one;
		}

		// A stretched rect's anchored position is the midpoint of its two offsets, so the rest and pressed
		// positions fall straight out of the insets ApplyLedge wrote.
		private Vector2 RestFacePosition
		{
			get
			{
				float ledge = Theme.Current.Layout.LetterpressLedge;
				return new Vector2(-ledge * 0.5f, ledge * 0.5f);
			}
		}

		private Vector2 PressedFacePosition
		{
			get
			{
				float ledge = Theme.Current.Layout.LetterpressLedge;
				return new Vector2(ledge * 0.5f, -ledge * 0.5f);
			}
		}

		private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.offsetMin = offsetMin;
			rect.offsetMax = offsetMax;
		}

		/// <summary>The button's click event. Named the way <see cref="Button"/> names its own, and no richer.</summary>
		[Serializable]
		public sealed class ClickEvent : UnityEvent
		{
		}
	}
}
