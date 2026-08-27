using System.Globalization;
using TMPro;
using UnityEngine;

namespace Assembler.Shell.Typography
{
	/// <summary>
	/// The drop cap, as arithmetic and two string edits. A paragraph opens with an outsized initial that hangs
	/// down through the first few lines, and the lines beside it are indented to clear it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Why it is not one pass.</b> TextMeshPro's <c>&lt;indent&gt;</c> runs from where it is opened until it
	/// is closed, and the place to close it — the first character of the line below the cap — is only known once
	/// the text has been laid out with the indent in force. So: open the indent, lay out, read where line N + 1
	/// begins, insert the closing tag there, lay out again.
	/// </para>
	/// <para>
	/// <b>Why it is exactly two passes, not a loop.</b> The closing tag is inserted at a line boundary, and
	/// everything above it is unchanged by the insertion — so the break positions the first pass measured are
	/// still the break positions after it. Nothing can shift, so nothing needs re-measuring. That is the whole
	/// reason the tag goes at the start of line N + 1 rather than, say, after the last word of line N.
	/// </para>
	/// <para>
	/// <b>Why the cap size is arithmetic rather than measured.</b> A drop cap's ink runs from the first line's
	/// cap line down to the Nth line's baseline. Both are functions of the font's <see cref="FaceInfo"/> and the
	/// body's size and leading — none of which depend on layout — so the size is computed before the first pass
	/// and never has to be revised.
	/// </para>
	/// <para>
	/// Static because none of it needs a component: <see cref="DropCap"/> is a wrapper that decides <em>when</em>
	/// to run it, not <em>how</em>.
	/// </para>
	/// </remarks>
	public static class DropCapFormatter
	{
		/// <summary>The tag closing an indent — inserted verbatim, and worth naming once.</summary>
		public const string CloseTag = "</indent>";

		/// <summary>
		/// The character that would be set as the cap: the paragraph's first non-whitespace one, or
		/// <c>'\0'</c> when there is nothing to drop.
		/// </summary>
		public static char CapCharacter(string? body)
		{
			int start = FirstVisibleIndex(body);

			return start < 0 ? '\0' : body![start];
		}

		/// <summary>
		/// The paragraph with its opening character removed and an indent of <paramref name="indentWidth"/> units
		/// opened in its place. This is the first pass's input.
		/// </summary>
		public static string OpenIndent(string? body, float indentWidth)
		{
			if (string.IsNullOrEmpty(body))
			{
				return string.Empty;
			}

			int start = FirstVisibleIndex(body);

			if (start < 0)
			{
				return body!;
			}

			string remainder = body!.Substring(start + 1);
			string width = indentWidth.ToString("0.###", CultureInfo.InvariantCulture);

			return "<indent=" + width + "px>" + remainder;
		}

		/// <summary>
		/// Where <see cref="CloseTag"/> belongs in the string <see cref="OpenIndent"/> produced: at the first
		/// character of line <paramref name="lineCount"/> (counting from zero, so the line below the cap).
		/// </summary>
		/// <returns>
		/// The index to insert at, or −1 when the paragraph is shorter than the cap is deep — in which case the
		/// indent runs to the end of the text, which is what it should do.
		/// </returns>
		public static int FindCloseIndex(TMP_TextInfo? textInfo, int lineCount)
		{
			if (textInfo == null || lineCount < 1 || textInfo.lineCount <= lineCount)
			{
				return -1;
			}

			int firstCharacter = textInfo.lineInfo[lineCount].firstCharacterIndex;

			if (firstCharacter < 0 || firstCharacter >= textInfo.characterInfo.Length)
			{
				return -1;
			}

			// characterInfo[i].index is the character's position in the string TextMeshPro parsed — which is the
			// string OpenIndent returned, tags and all. That is exactly the string being edited.
			return textInfo.characterInfo[firstCharacter].index;
		}

		/// <summary>Closes the indent at <paramref name="index"/>. This is the second pass's input.</summary>
		public static string CloseIndent(string opened, int index)
		{
			if (index < 0 || index > opened.Length)
			{
				return opened;
			}

			return opened.Insert(index, CloseTag);
		}

		/// <summary>
		/// The point size at which <paramref name="capFont"/> sets a cap whose ink spans <paramref name="lineCount"/>
		/// lines of the given body: from the first line's cap line down to the last line's baseline.
		/// </summary>
		public static float CapPointSize(
			TMP_FontAsset? capFont,
			TMP_FontAsset? bodyFont,
			float bodyPointSize,
			float bodyLineSpacing,
			int lineCount)
		{
			if (capFont == null || bodyFont == null || lineCount < 1)
			{
				return bodyPointSize;
			}

			float capHeightPerPoint = CapHeightPerPoint(capFont);

			if (capHeightPerPoint <= 0f)
			{
				return bodyPointSize;
			}

			float lineHeight = LineHeight(bodyFont, bodyPointSize, bodyLineSpacing);
			float bodyCapHeight = CapHeightPerPoint(bodyFont) * bodyPointSize;
			float ink = ((lineCount - 1) * lineHeight) + bodyCapHeight;

			return ink / capHeightPerPoint;
		}

		/// <summary>
		/// How far below the top of a text rect the first line's baseline sits, for a top-aligned label — the
		/// number that lets a cap be dropped onto a chosen baseline of the paragraph beside it.
		/// </summary>
		public static float Ascender(TMP_FontAsset? font, float pointSize)
		{
			return font == null ? 0f : font.faceInfo.ascentLine * Scale(font, pointSize);
		}

		/// <summary>One line of <paramref name="font"/> at the given size and leading, in canvas units.</summary>
		public static float LineHeight(TMP_FontAsset? font, float pointSize, float lineSpacing)
		{
			if (font == null)
			{
				return 0f;
			}

			// TextMeshPro adds lineSpacing in hundredths of an em on top of the face's own line height, which is
			// how the theme's text styles express leading.
			return (font.faceInfo.lineHeight * Scale(font, pointSize)) + (lineSpacing * pointSize * 0.01f);
		}

		// The distance from baseline to cap line, as a fraction of the point size. FaceInfo carries both in the
		// font units the face was sampled at, so the ratio is size-independent.
		private static float CapHeightPerPoint(TMP_FontAsset font)
		{
			var face = font.faceInfo;

			if (face.pointSize <= 0)
			{
				return 0f;
			}

			return Mathf.Max(0f, face.capLine - face.baseline) / face.pointSize * face.scale;
		}

		// Leading whitespace would be dropped as the cap otherwise — an invisible glyph and an indent that looks
		// like a bug.
		private static int FirstVisibleIndex(string? body)
		{
			if (string.IsNullOrEmpty(body))
			{
				return -1;
			}

			for (int i = 0; i < body!.Length; i++)
			{
				if (!char.IsWhiteSpace(body[i]))
				{
					return i;
				}
			}

			return -1;
		}

		private static float Scale(TMP_FontAsset font, float pointSize)
		{
			var face = font.faceInfo;

			return face.pointSize <= 0 ? 0f : pointSize / face.pointSize * face.scale;
		}
	}
}
