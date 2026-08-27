using UnityEngine;
using UnityEngine.UI;

namespace Assembler.Shell.Controls
{
	/// <summary>
	/// The only thing in the shell that a pointer is allowed to hit. An invisible, stationary rect of at least
	/// <see cref="Theming.LayoutSettings.MinHitTarget"/> units that sits alongside the art it stands in for.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The rule (UIPLAN 7.4) is <em>nothing raycasts except things named HitTarget</em>, and every decorative
	/// graphic sets <c>raycastTarget = false</c>. Two things fall out of it. Touch areas stop being an accident
	/// of how big the art happens to be — a 20-unit pause glyph still gets its 44 units of tap area. And a
	/// pressed control can animate freely, because the thing being hit never moves: a face sliding onto its
	/// plate under the pointer cannot slide out from under it and cancel the press.
	/// </para>
	/// <para>
	/// It is a <see cref="MaskableGraphic"/> drawing a fully transparent quad rather than a graphic that draws
	/// nothing at all. A <see cref="CanvasRenderer"/> with no geometry reports a depth of −1, and
	/// <see cref="GraphicRaycaster"/> skips those — so "draws nothing" and "is hittable" are mutually exclusive
	/// in uGUI. The quad costs one transparent batch entry, which is what an invisible <see cref="Image"/> would
	/// have cost anyway, without the sprite and material behind it.
	/// </para>
	/// <para>
	/// Maskable so that a hit target scrolled out of a <see cref="RectMask2D"/> is culled along with the row it
	/// belongs to, rather than staying tappable off-screen.
	/// </para>
	/// </remarks>
	[DisallowMultipleComponent]
	[AddComponentMenu("Assembler/Shell/Hit Target")]
	public sealed class HitTarget : MaskableGraphic
	{
		protected override void OnEnable()
		{
			base.OnEnable();
			ForceInvisible();
		}

#if UNITY_EDITOR
		protected override void Reset()
		{
			base.Reset();
			raycastTarget = true;
			ForceInvisible();
		}

		protected override void OnValidate()
		{
			base.OnValidate();
			ForceInvisible();
		}
#endif

		// The colour is not an authoring decision: a hit target that can be tinted is a hit target somebody will
		// eventually tint, and then the shell has an invisible rectangle that isn't.
		private void ForceInvisible()
		{
			if (color != Color.clear)
			{
				color = Color.clear;
			}
		}
	}
}
