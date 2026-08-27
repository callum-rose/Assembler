using System;
using Assembler.Shell.Controls;
using Assembler.Shell.Motion;
using Assembler.Shell.Theming;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assembler.Shell.Navigation
{
	/// <summary>
	/// The <b>V</b> of the shell's MVP: the root component of a screen prefab. It exposes a bind surface and
	/// raises events; it knows nothing about the model, the stack, or what its buttons mean (UIPLAN 4.1).
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The root carries its own <see cref="Canvas"/> and <see cref="GraphicRaycaster"/></b> (UIPLAN 2.1). The
	/// canvas isolates this screen's layout rebuilds from every other screen's and makes showing or hiding it a
	/// single cheap toggle; the raycaster is not optional alongside it, because a <c>GraphicRaycaster</c> does
	/// not see into a nested canvas — without one of its own, nothing on the screen is tappable.
	/// </para>
	/// <para>
	/// <b>The back affordance is the navigator's, not the screen's.</b> Its label is the title of the entry
	/// beneath the top of the stack (UIPLAN 3.3), which is a fact about the stack rather than about this page —
	/// so the screen only offers the button, and the navigator labels it, shows it and wires it.
	/// </para>
	/// </remarks>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(RectTransform))]
	[RequireComponent(typeof(Canvas))]
	[RequireComponent(typeof(GraphicRaycaster))]
	[RequireComponent(typeof(CanvasGroup))]
	public abstract class ScreenView : MonoBehaviour
	{
		[Header("Screen")]
		[Tooltip("Drives the transition, and gates input while one is running.")]
		[SerializeField] private CanvasGroup canvasGroup = null!;

		[Tooltip("The screen's own title. The navigator writes the catalog's title into it. Optional.")]
		[SerializeField] private TMP_Text? title;

		[Tooltip("The back control. The navigator labels it, shows it and wires it. Absent on a root screen.")]
		[SerializeField] private LetterpressButton? backButton;

		private Tween? _fade;

		/// <summary>
		/// The presenter built alongside this view, or null for a screen that is pure chrome. Overridden for
		/// free by deriving from <see cref="ScreenView{TPresenter}"/>.
		/// </summary>
		public virtual Type? PresenterType => null;

		/// <summary>
		/// Whether the instance survives being left (UIPLAN 3.2). True by default — a cached screen keeps its
		/// scroll position and its search text for nothing. Override to false for a screen expensive enough to
		/// be worth rebuilding rather than holding.
		/// </summary>
		public virtual bool KeepAlive => true;

		/// <summary>The back control, for the navigator to label and wire. Null on a screen that has none.</summary>
		public LetterpressButton? BackButton => backButton;

		/// <summary>The screen's title, as the catalog names it.</summary>
		public string Title
		{
			get => title == null ? string.Empty : title.text;
			set
			{
				if (title != null)
				{
					title.SetText(value);
				}
			}
		}

		/// <summary>
		/// Puts the screen in the state a transition starts from — invisible and deaf — before it is activated,
		/// so that activating it cannot flash a fully-drawn page for the frame before the fade begins.
		/// </summary>
		public void PrepareEnter()
		{
			canvasGroup.alpha = 0f;
			SetInteractive(false);
		}

		/// <summary>Whether the screen answers the pointer. Off for the length of a transition.</summary>
		public void SetInteractive(bool interactive)
		{
			canvasGroup.interactable = interactive;
			canvasGroup.blocksRaycasts = interactive;
		}

		/// <summary>Brings the screen in. The navigator awaits this before the screen counts as arrived.</summary>
		public virtual Awaitable OnEnter()
		{
			return FadeTo(1f);
		}

		/// <summary>Takes the screen out. The navigator awaits this before the next one begins to arrive.</summary>
		public virtual Awaitable OnExit()
		{
			return FadeTo(0f);
		}

		/// <summary>The default transition: a fade at the theme's screen-transition timing.</summary>
		protected Awaitable FadeTo(float alpha)
		{
			var spec = Theme.Current.Motion.ScreenTransition;

			// Killing the previous fade resolves whatever was awaiting it, which is what stops an interrupted
			// transition from leaving the navigator waiting on a tween that will never finish.
			_fade?.Kill();

			_fade = canvasGroup
				.TweenAlpha(alpha, spec.Duration)
				.SetMotion(spec)
				.SetShellDefaults(gameObject);

			return _fade.ToAwaitable();
		}

		protected virtual void OnDisable()
		{
			// The kill-on-disable link has already stopped it; dropping the handle keeps the next transition
			// from reading a tween that is on its way out.
			_fade = null;
		}

		protected virtual void Reset()
		{
			canvasGroup = GetComponent<CanvasGroup>();
		}
	}
}
