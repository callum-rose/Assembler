using TMPro;
using UnityEngine;

namespace Assembler.Shell.Theming.Binders
{
	/// <summary>
	/// Sets the font, size, case, tracking, leading and colour of the <see cref="TMP_Text"/> on this object from a
	/// named <see cref="TextStyleId"/>. A style already carries a colour role, so a label wants this binder
	/// <em>instead of</em> <see cref="ThemeColor"/>, not as well.
	/// </summary>
	[ExecuteAlways]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(TMP_Text))]
	[AddComponentMenu("Assembler/Shell/Text Style")]
	public sealed class TextStyleBinder : MonoBehaviour
	{
		[SerializeField] private TextStyleId style = TextStyleId.Body;

		private TMP_Text? _text;

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

		/// <summary>Restyles the label from the theme in force.</summary>
		public void Apply()
		{
			_text = _text != null ? _text : GetComponent<TMP_Text>();

			if (_text == null)
			{
				return;
			}

			Theme.Current.ApplyStyle(style, _text);
		}
	}
}
