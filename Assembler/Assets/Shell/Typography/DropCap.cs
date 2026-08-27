using Assembler.Shell.Theming;
using TMPro;
using UnityEngine;

namespace Assembler.Shell.Typography
{
	/// <summary>
	/// Drops the first letter of the paragraph on this object into an outsized cap beside it, and indents the
	/// lines it hangs through so they clear it. The arithmetic and the string edits are
	/// <see cref="DropCapFormatter"/>'s; this decides when to run them.
	/// </summary>
	/// <remarks>
	/// <para>
	/// It hooks TextMeshPro as an <see cref="ITextPreprocessor"/>, so the paragraph on the label stays the
	/// paragraph as authored — the indent tags exist only in what TextMeshPro parses. Anything reading or
	/// writing <c>text</c> sees clean copy, which matters because a presenter binds this label like any other.
	/// </para>
	/// <para>
	/// <b>The cap wears no <see cref="Binders.TextStyleBinder"/>.</b> A binder would set the cap's point size
	/// from the theme, and its size is computed — the two would fight, and which won would come down to
	/// component enable order. So this applies the named style itself and then overrides the size, in that
	/// order, every time.
	/// </para>
	/// </remarks>
	[ExecuteAlways]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(TMP_Text))]
	[AddComponentMenu("Assembler/Shell/Drop Cap")]
	public sealed class DropCap : MonoBehaviour, ITextPreprocessor
	{
		[Tooltip("The cap glyph. A child of this label, anchored to its top-left corner.")]
		[SerializeField] private TMP_Text cap = null!;

		[Tooltip("The theme style the cap takes its font and colour from. Its size is computed, not read.")]
		[SerializeField] private TextStyleId? capStyle;

		[Tooltip("How many lines the cap hangs through.")]
		[Min(1)]
		[SerializeField] private int lineCount = 2;

		[Tooltip("Space between the cap and the text beside it, in canvas units.")]
		[Min(0f)]
		[SerializeField] private float gutter = 8f;

		private TMP_Text? _body;
		private RectTransform? _capRect;
		private string _opened = string.Empty;
		private string _lastSource = string.Empty;
		private float _indentWidth;
		private int _closeIndex = -1;
		private bool _rebuilding;
		private bool _dirty = true;

		/// <summary>The paragraph, as authored — no indent tags, and with its opening letter still in it.</summary>
		/// <remarks>
		/// <b>Write the paragraph through here, never through <c>TMP_Text.SetText</c>.</b> <c>SetText</c> marks
		/// the label's input source as a pre-parsed buffer, and TextMeshPro skips the preprocessor for those —
		/// so a drop-capped paragraph set that way silently loses its cap and its indent. Assigning
		/// <c>text</c>, which is what this does, marks it as a string and keeps the preprocessor in the loop.
		/// </remarks>
		public string Text
		{
			get => Body == null ? string.Empty : Body.text;
			set
			{
				if (Body == null)
				{
					return;
				}

				Body.text = value;
				_dirty = true;
			}
		}

		private void OnEnable()
		{
			if (Body != null)
			{
				Body.textPreprocessor = this;
			}

			Theme.Changed += MarkDirty;
			_dirty = true;
		}

		private void OnDisable()
		{
			Theme.Changed -= MarkDirty;

			if (Body != null && ReferenceEquals(Body.textPreprocessor, this))
			{
				Body.textPreprocessor = null;
			}
		}

		private void OnRectTransformDimensionsChange()
		{
			MarkDirty();
		}

		// Deferred to the end of the frame rather than run on the spot: laying the paragraph out can resize this
		// rect, which lands straight back in OnRectTransformDimensionsChange. Settling here costs a bool test a
		// frame and cannot recurse.
		private void LateUpdate()
		{
			if (Body != null && !ReferenceEquals(Body.text, _lastSource))
			{
				_dirty = true;
			}

			if (!_dirty)
			{
				return;
			}

			Rebuild();
		}

		private void OnValidate()
		{
			_dirty = true;
		}

		/// <inheritdoc/>
		/// <remarks>
		/// Called by TextMeshPro every time it parses. It reads the two numbers <see cref="Rebuild"/> worked out
		/// rather than working them out itself, which is what keeps the pass count at two.
		/// </remarks>
		public string PreprocessText(string text)
		{
			_lastSource = text;
			_opened = DropCapFormatter.OpenIndent(text, _indentWidth);

			return _closeIndex >= 0 ? DropCapFormatter.CloseIndent(_opened, _closeIndex) : _opened;
		}

		/// <summary>Re-sizes the cap, re-lays the paragraph around it and drops the cap onto its baseline.</summary>
		public void Rebuild()
		{
			if (_rebuilding || Body == null || cap == null)
			{
				return;
			}

			_rebuilding = true;
			_dirty = false;

			try
			{
				SizeCap();

				// Pass one: indent open, never closed — so every line is indented and the layout tells us where
				// the line below the cap begins.
				_closeIndex = -1;
				Body.ForceMeshUpdate();

				_closeIndex = DropCapFormatter.FindCloseIndex(Body.textInfo, lineCount);

				// Pass two: the same string with the indent closed at that line boundary. Nothing above the
				// insertion moves, so this is the last pass there is.
				if (_closeIndex >= 0)
				{
					Body.ForceMeshUpdate();
				}

				PlaceCap();
				_lastSource = Body.text;
			}
			finally
			{
				_rebuilding = false;
			}
		}

		private void SizeCap()
		{
			if (Body == null || cap == null)
			{
				return;
			}

			if (capStyle != null)
			{
				Theme.Current.ApplyStyle(capStyle, cap);
			}

			cap.textWrappingMode = TextWrappingModes.NoWrap;
			cap.overflowMode = TextOverflowModes.Overflow;
			cap.alignment = TextAlignmentOptions.TopLeft;
			cap.raycastTarget = false;

			cap.fontSize = DropCapFormatter.CapPointSize(
				cap.font,
				Body.font,
				Body.fontSize,
				Body.lineSpacing,
				lineCount);

			// Read straight off the paragraph rather than waiting for PreprocessText to report it: the cap has to
			// be measured before the first pass, and the first pass is what would have told us.
			char capCharacter = DropCapFormatter.CapCharacter(Body.text);
			bool hasCap = capCharacter != '\0';

			cap.SetText(hasCap ? capCharacter.ToString() : string.Empty);
			cap.gameObject.SetActive(hasCap);

			if (!hasCap)
			{
				_indentWidth = 0f;
				return;
			}

			var preferred = cap.GetPreferredValues(cap.text);
			CapRect?.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, preferred.x);
			CapRect?.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferred.y);

			_indentWidth = preferred.x + gutter;
		}

		// The cap sits on the baseline of the last line it hangs through, which is what makes it read as part of
		// the paragraph rather than a graphic parked next to it.
		private void PlaceCap()
		{
			if (Body == null || cap == null || CapRect == null)
			{
				return;
			}

			var info = Body.textInfo;
			int line = Mathf.Min(lineCount, info.lineCount) - 1;

			if (line < 0)
			{
				return;
			}

			float baseline = info.lineInfo[line].baseline;
			float ascender = DropCapFormatter.Ascender(cap.font, cap.fontSize);
			var bodyRect = (RectTransform)Body.transform;

			// Vector2 is forced here by the RectTransform anchored-position API.
			CapRect.anchoredPosition = new Vector2(0f, baseline - bodyRect.rect.yMax + ascender);
		}

		private void MarkDirty()
		{
			_dirty = true;
		}

		private TMP_Text? Body => _body = _body != null ? _body : GetComponent<TMP_Text>();

		private RectTransform? CapRect
		{
			get
			{
				if (cap == null)
				{
					return null;
				}

				return _capRect = _capRect != null ? _capRect : (RectTransform)cap.transform;
			}
		}
	}
}
