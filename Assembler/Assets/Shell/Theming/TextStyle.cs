using System;
using TMPro;
using UnityEngine;

namespace Assembler.Shell.Theming
{
	/// <summary>
	/// One entry of the shell's typographic scale: the font cut, size, case, tracking, leading and colour role
	/// that a <see cref="TextStyleId"/> resolves to. Authored on the <see cref="ShellTheme"/> asset and applied
	/// to a label by <see cref="Binders.TextStyleBinder"/>.
	/// </summary>
	[Serializable]
	public sealed class TextStyle
	{
		[SerializeField] private TextStyleId? id;
		[SerializeField] private TMP_FontAsset? font;

		[Tooltip("Size in shell units. The canvas is 390 units across, so these are the prototype's CSS pixels 1:1.")]
		[SerializeField] private float fontSize = 15f;

		[SerializeField] private bool bold;
		[SerializeField] private bool italic;
		[SerializeField] private TextCase textCase = TextCase.AsTyped;

		[Tooltip("Tracking, in hundredths of an em — CSS letter-spacing .2em is 20 here.")]
		[SerializeField] private float characterSpacing;

		[Tooltip("Leading added on top of the font's own line height, in hundredths of an em. Newsreader's " +
			"line height is exactly 1em, so CSS line-height 1.52 is 52 here.")]
		[SerializeField] private float lineSpacing;

		[Tooltip("The palette role this style's labels take their colour from.")]
		[SerializeField] private ColorRole? color;

		public TextStyleId? Id => id;

		public TMP_FontAsset? Font => font;

		public float FontSize => fontSize;

		public ColorRole? Color => color;

		/// <summary>
		/// Paints <paramref name="text"/> with this style. The colour is passed in because a style names a
		/// <see cref="ColorRole"/>, and only the theme knows what that role currently resolves to.
		/// </summary>
		public void ApplyTo(TMP_Text text, Color resolvedColor)
		{
			if (text == null)
			{
				return;
			}

			if (font != null)
			{
				text.font = font;
			}

			text.fontSize = fontSize;
			text.fontStyle = FontStyles;
			text.characterSpacing = characterSpacing;
			text.lineSpacing = lineSpacing;
			text.color = resolvedColor;
		}

		private FontStyles FontStyles
		{
			get
			{
				var styles = TMPro.FontStyles.Normal;

				if (bold)
				{
					styles |= TMPro.FontStyles.Bold;
				}

				if (italic)
				{
					styles |= TMPro.FontStyles.Italic;
				}

				return styles | textCase switch
				{
					TextCase.UpperCase => TMPro.FontStyles.UpperCase,
					TextCase.LowerCase => TMPro.FontStyles.LowerCase,
					TextCase.SmallCaps => TMPro.FontStyles.SmallCaps,
					_ => TMPro.FontStyles.Normal
				};
			}
		}
	}
}
