using Assembler.Shell.Theming;
using UnityEngine;
using UnityEngine.UI;

namespace Assembler.Shell.Controls
{
	/// <summary>
	/// A horizontal rule of one of the newspaper's three weights, sized from the theme rather than authored.
	/// The colour is not its business: the graphics carry <see cref="Binders.ThemeColor"/> like everything else,
	/// so the same prefab is a quiet <c>Rule</c> hairline or a hard <c>RuleHard</c> one.
	/// </summary>
	/// <remarks>
	/// It writes its own height and reports it as an <see cref="ILayoutElement"/>, so it measures correctly both
	/// as an anchored fixture and as a child of a layout group. A layout group that controls its children's
	/// heights simply overwrites the height afterwards with the same number.
	/// </remarks>
	[ExecuteAlways]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(RectTransform))]
	[AddComponentMenu("Assembler/Shell/Rule")]
	public sealed class Rule : MonoBehaviour, ILayoutElement
	{
		[SerializeField] private RuleWeight weight = RuleWeight.Hairline;

		[Tooltip("The upper line. A hairline or heavy rule is drawn by this one alone.")]
		[SerializeField] private RectTransform line = null!;

		[Tooltip("The lower line of a double rule. Hidden at the other two weights.")]
		[SerializeField] private RectTransform? secondLine;

		private RectTransform? _rect;

		/// <summary>How heavily the rule is struck. Setting it re-sizes immediately.</summary>
		public RuleWeight Weight
		{
			get => weight;
			set
			{
				weight = value;
				Apply();
			}
		}

		/// <summary>The rule's total height in canvas units, as the theme currently measures it.</summary>
		public float Thickness
		{
			get
			{
				var layout = Theme.Current.Layout;

				return weight switch
				{
					RuleWeight.Heavy => layout.HeavyRule,
					// Two hairlines and a gap of one and a half between them: the printed double rule reads as
					// four units of structure, which is what the prototype's `4px double` renders as.
					RuleWeight.Double => (layout.Hairline * 2f) + (layout.Hairline * 1.5f),
					_ => layout.Hairline
				};
			}
		}

		float ILayoutElement.minWidth => -1f;

		float ILayoutElement.preferredWidth => -1f;

		float ILayoutElement.flexibleWidth => -1f;

		float ILayoutElement.minHeight => Thickness;

		float ILayoutElement.preferredHeight => Thickness;

		float ILayoutElement.flexibleHeight => -1f;

		int ILayoutElement.layoutPriority => 1;

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
			// Applying writes rect sizes, which is not allowed from inside a validation callback.
			Deferred.Run(this, Apply);
		}

		void ILayoutElement.CalculateLayoutInputHorizontal()
		{
		}

		void ILayoutElement.CalculateLayoutInputVertical()
		{
		}

		/// <summary>Re-reads the theme's measurements and re-strikes the rule.</summary>
		public void Apply()
		{
			_rect = _rect != null ? _rect : GetComponent<RectTransform>();

			if (_rect == null || line == null)
			{
				return;
			}

			float hairline = Theme.Current.Layout.Hairline;
			bool doubled = weight == RuleWeight.Double;

			_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Thickness);

			// Vector2 is forced here by the RectTransform anchor API.
			line.anchorMin = new Vector2(0f, 1f);
			line.anchorMax = new Vector2(1f, 1f);
			line.pivot = new Vector2(0.5f, 1f);
			line.offsetMin = new Vector2(0f, 0f);
			line.offsetMax = new Vector2(0f, 0f);
			line.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, doubled ? hairline : Thickness);
			line.anchoredPosition = Vector2.zero;

			if (secondLine == null)
			{
				return;
			}

			// Laid out whether or not it is showing: an inactive line left at whatever size it was authored with
			// reads as a mistake in the prefab, and it would have to be laid out on the frame the weight changed
			// anyway.
			secondLine.gameObject.SetActive(doubled);

			secondLine.anchorMin = new Vector2(0f, 0f);
			secondLine.anchorMax = new Vector2(1f, 0f);
			secondLine.pivot = new Vector2(0.5f, 0f);
			secondLine.offsetMin = new Vector2(0f, 0f);
			secondLine.offsetMax = new Vector2(0f, 0f);
			secondLine.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, hairline);
			secondLine.anchoredPosition = Vector2.zero;
		}
	}
}
