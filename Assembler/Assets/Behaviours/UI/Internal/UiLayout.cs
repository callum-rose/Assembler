using Assembler.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace Assembler.Behaviours.UI.Internal
{
	/// <summary>
	/// Shared uGUI plumbing for the composable UI blocks: ensuring a <see cref="RectTransform"/> on an
	/// entity GameObject, expressing preferred size to a parent layout group, and instantiating a leaf
	/// block's prefab as a stretch-to-fill child.
	/// </summary>
	public static class UiLayout
	{
		/// <summary>
		/// Returns the GameObject's <see cref="RectTransform"/>, upgrading its plain <see cref="Transform"/>
		/// to one if needed. Entity GameObjects are created with a plain Transform; UI blocks need a
		/// RectTransform to participate in canvas layout.
		/// </summary>
		public static RectTransform EnsureRectTransform(GameObject go) =>
			go.transform as RectTransform ?? go.AddComponent<RectTransform>();

		/// <summary>Anchors a RectTransform to fill its parent edge-to-edge.</summary>
		public static void StretchToFill(RectTransform rt)
		{
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
		}

		/// <summary>
		/// Anchors a RectTransform to fill the screen's safe area — the region clear of notches, punch-holes
		/// and rounded corners — rather than the literal screen edge. Use for content sitting directly under a
		/// screen-space-overlay canvas so it isn't clipped on mobile; off mobile the safe area is the full
		/// screen, so this behaves like <see cref="StretchToFill"/>. A <see cref="SafeAreaFitter"/> keeps the
		/// anchors in step as the safe area changes (orientation flips, resolution changes).
		/// </summary>
		public static void StretchToSafeArea(RectTransform rt)
		{
			StretchToFill(rt);
			rt.gameObject.GetOrAddComponent<SafeAreaFitter>();
		}

		/// <summary>
		/// Adds/updates a <see cref="LayoutElement"/> so a parent layout group sizes this element. Each
		/// dimension is only constrained when the descriptor provides a value &gt; 0; otherwise the
		/// layout/content decides.
		/// </summary>
		public static void ApplyPreferredSize(GameObject go, float preferredWidth, float preferredHeight)
		{
			if (preferredWidth <= 0f && preferredHeight <= 0f)
			{
				return;
			}

			var element = go.GetOrAddComponent<LayoutElement>();

			if (preferredWidth > 0f)
			{
				element.preferredWidth = preferredWidth;
			}

			if (preferredHeight > 0f)
			{
				element.preferredHeight = preferredHeight;
			}
		}

		/// <summary>
		/// Instantiates a leaf block's <paramref name="prefab"/> as a stretch-to-fill child of
		/// <paramref name="host"/> and returns the typed view component on its root.
		/// </summary>
		public static TView InstantiateView<TView>(GameObject prefab, RectTransform host)
			where TView : Component
		{
			var instance = Object.Instantiate(prefab, host, worldPositionStays: false);
			var rt = instance.transform as RectTransform ?? instance.AddComponent<RectTransform>();
			StretchToFill(rt);

			var view = instance.GetComponent<TView>();
			if (view == null)
			{
				throw new MissingComponentException(
					$"UI prefab '{prefab.name}' is missing the expected '{typeof(TView).Name}' view component.");
			}

			return view;
		}
	}
}
