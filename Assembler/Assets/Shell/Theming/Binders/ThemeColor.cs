using UnityEngine;
using UnityEngine.UI;

namespace Assembler.Shell.Theming.Binders
{
	/// <summary>
	/// Paints the <see cref="Graphic"/> on this object from a <see cref="ColorRole"/>. Every graphic in the shell
	/// carries one of these rather than an authored colour, so swapping the theme asset re-skins the app.
	/// Applies on enable and on every theme change, and previews live in the editor.
	/// </summary>
	[ExecuteAlways]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Graphic))]
	[AddComponentMenu("Assembler/Shell/Theme Colour")]
	public sealed class ThemeColor : MonoBehaviour
	{
		[SerializeField] private ColorRole? role;

		[Tooltip("Multiplies the role's alpha. For the tints the palette doesn't name — a rule at 35%, say.")]
		[Range(0f, 1f)]
		[SerializeField] private float alpha = 1f;

		private Graphic? _graphic;

		private void OnEnable()
		{
			Theme.Changed += Apply;
			Apply();
		}

		private void OnDisable()
		{
			Theme.Changed -= Apply;
		}

		private void OnValidate()
		{
			Apply();
		}

		/// <summary>Repaints the graphic from the theme in force.</summary>
		public void Apply()
		{
			// A component that has just been added has no role yet. Leaving the authored colour alone beats
			// flooding the scene with magenta and a warning per repaint while it is still being wired up.
			if (role == null)
			{
				return;
			}

			_graphic = _graphic != null ? _graphic : GetComponent<Graphic>();

			if (_graphic == null)
			{
				return;
			}

			var color = Theme.Current.GetColor(role);
			color.a *= alpha;
			_graphic.color = color;
		}
	}
}
