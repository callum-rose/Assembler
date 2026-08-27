using System;
using UnityEngine;

namespace Assembler.Shell.Theming
{
	/// <summary>
	/// The measurements the shell's chrome is built from, in canvas units. The canvas is 390 units across the
	/// short axis, so these transfer 1:1 from the prototype's CSS pixels.
	/// </summary>
	[Serializable]
	public sealed class LayoutSettings
	{
		[Tooltip("Side margin of the masthead, hero and feed grid.")]
		[SerializeField] private float pageGutter = 20f;

		[Tooltip("Side margin of the denser list surfaces: subhead, search, archive rows, settings rows.")]
		[SerializeField] private float listGutter = 16f;

		[Tooltip("A hairline rule between cells and rows.")]
		[SerializeField] private float hairline = 1f;

		[Tooltip("The heavy rule under the masthead and a section header.")]
		[SerializeField] private float heavyRule = 3f;

		[Tooltip("The ledge a letterpress element casts, and the distance a press travels to consume it.")]
		[SerializeField] private float letterpressLedge = 4f;

		[Tooltip("Corner radius. The letterpress look is nearly square.")]
		[SerializeField] private float cornerRadius = 2f;

		[Tooltip("The rule an outlined element draws around itself — a quiet button, a stat band cell.")]
		[SerializeField] private float outlineWidth = 1.5f;

		[Tooltip("Minimum size of a HitTarget. Nothing in the shell is tappable below this.")]
		[SerializeField] private float minHitTarget = 44f;

		[Tooltip("Narrowest a feed card is allowed to be. The grid takes max(2, floor(width / this)) columns.")]
		[SerializeField] private float minFeedCellWidth = 170f;

		public float PageGutter => pageGutter;

		public float ListGutter => listGutter;

		public float Hairline => hairline;

		public float HeavyRule => heavyRule;

		public float LetterpressLedge => letterpressLedge;

		public float CornerRadius => cornerRadius;

		public float OutlineWidth => outlineWidth;

		public float MinHitTarget => minHitTarget;

		public float MinFeedCellWidth => minFeedCellWidth;
	}
}
