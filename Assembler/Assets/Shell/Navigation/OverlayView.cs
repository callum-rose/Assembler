using System;
using Assembler.Shell.Controls;
using Assembler.Shell.Motion;
using UnityEngine;
using UnityEngine.UI;

namespace Assembler.Shell.Navigation
{
	/// <summary>
	/// The root component of an overlay prefab: the pause sheet, the result slip, the launch overlay. Like a
	/// screen it is a passive view — it animates, it binds and it asks to be closed; it does not close itself.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Whether a tap outside closes it is the overlay's business.</b> A pause sheet says yes; a launch
	/// overlay mid-download says no. <see cref="SheetFrame"/> only reports the tap, and
	/// <c>dismissOnBackgroundTap</c> here is what turns it into a request.
	/// </para>
	/// <para>
	/// <b>Requesting is not closing.</b> <see cref="Dismissed"/> is a request to the overlay service, which owns
	/// the slot and the ordering — so an overlay that must confirm first, or must finish writing something, has
	/// somewhere to put that.
	/// </para>
	/// </remarks>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(RectTransform))]
	[RequireComponent(typeof(Canvas))]
	[RequireComponent(typeof(GraphicRaycaster))]
	public abstract class OverlayView : MonoBehaviour
	{
		[Header("Overlay")]
		[Tooltip("The chrome this overlay wears. Empty for an overlay that animates itself.")]
		[SerializeField] private SheetFrame? frame;

		[Tooltip("Whether a tap on the scrim asks for the overlay to be closed.")]
		[SerializeField] private bool dismissOnBackgroundTap = true;

		/// <summary>Raised when the overlay asks to be closed. It does not close itself.</summary>
		public event Action Dismissed = delegate { };

		/// <summary>Brings the overlay in. Awaited before it counts as shown.</summary>
		public virtual Awaitable OnShow()
		{
			return frame == null ? Awaitables.Completed() : frame.Show();
		}

		/// <summary>Takes the overlay out. Awaited before the slot is free again.</summary>
		public virtual Awaitable OnDismiss()
		{
			return frame == null ? Awaitables.Completed() : frame.Dismiss();
		}

		/// <summary>Asks to be closed — what a close button calls.</summary>
		protected void RequestDismiss()
		{
			Dismissed.Invoke();
		}

		protected virtual void OnEnable()
		{
			if (frame != null)
			{
				frame.Dismissed += OnBackgroundTapped;
			}
		}

		protected virtual void OnDisable()
		{
			if (frame != null)
			{
				frame.Dismissed -= OnBackgroundTapped;
			}
		}

		private void OnBackgroundTapped()
		{
			if (dismissOnBackgroundTap)
			{
				RequestDismiss();
			}
		}
	}
}
