using System;
using UnityEngine;

namespace Assembler.Shell.Navigation
{
	/// <summary>
	/// The overlay layer's own show/dismiss API (UIPLAN 3.6). Deliberately not the navigator: an overlay is not
	/// a place you can be, so it has no stack, no back, and no history.
	/// </summary>
	/// <remarks>
	/// One slot. Showing an overlay while another is up closes that one first, rather than stacking sheets on
	/// sheets — a paper can only interrupt you with one thing at a time.
	/// </remarks>
	public interface IOverlayService
	{
		/// <summary>What is showing, or null.</summary>
		OverlayId? Current { get; }

		/// <summary>Whether the slot is occupied.</summary>
		bool IsShowing { get; }

		/// <summary>Shows <paramref name="id"/>, binding it through <paramref name="configure"/> first.</summary>
		/// <remarks>
		/// The overlay is bound before its entrance, not after, so it is never briefly visible and blank. The
		/// callback is typed because an overlay is opened from one place that knows exactly what it is — unlike
		/// a screen, which is pushed by id from anywhere and so takes its argument as a record.
		/// </remarks>
		Awaitable Show<TOverlay>(OverlayId id, Action<TOverlay>? configure = null) where TOverlay : OverlayView;

		/// <summary>Shows <paramref name="id"/> with nothing to bind.</summary>
		Awaitable Show(OverlayId id);

		/// <summary>Closes whatever is showing. A no-op when nothing is.</summary>
		Awaitable Dismiss();
	}
}
