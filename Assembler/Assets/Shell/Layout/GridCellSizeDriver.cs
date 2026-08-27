using Assembler.Shell.Theming;
using UnityEngine;
using UnityEngine.UI;

namespace Assembler.Shell.Layout
{
	/// <summary>
	/// Sizes a <see cref="GridLayoutGroup"/>'s cells to fill the width it has been given, taking as many columns
	/// as fit: <c>columns = max(minColumns, floor(width / minCellWidth))</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A <see cref="GridLayoutGroup"/> only knows fixed cell sizes, so a feed authored at a fixed 2 × 170 would
	/// either overflow a narrow phone or leave a tablet with two enormous cards. Driving the cell size instead
	/// makes the grid column-aware from the start; the extra columns simply activate when the wider layouts land
	/// (UIPLAN 6.3, <see href="https://github.com/callum-rose/Assembler/issues/572">#572</see>).
	/// </para>
	/// <para>
	/// Cell height comes from <see cref="cellAspect"/> rather than from the content, because a grid's children
	/// are fixed-size by rule: exactly one <see cref="ContentSizeFitter"/> lives per scroll, at the content root,
	/// and nothing below it grows (UIPLAN 6.2). A card whose title runs long clamps; it does not push its
	/// neighbours out of alignment.
	/// </para>
	/// </remarks>
	[ExecuteAlways]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(GridLayoutGroup))]
	[AddComponentMenu("Assembler/Shell/Grid Cell Size Driver")]
	public sealed class GridCellSizeDriver : MonoBehaviour
	{
		[Tooltip("Narrowest a cell may be. Zero takes the theme's own minimum feed cell width.")]
		[Min(0f)]
		[SerializeField] private float minCellWidth;

		[Tooltip("Fewest columns to take, however narrow the grid gets.")]
		[Min(1)]
		[SerializeField] private int minColumns = 2;

		[Tooltip("Cell height as a multiple of cell width. Zero leaves the authored height alone.")]
		[Min(0f)]
		[SerializeField] private float cellAspect;

		private GridLayoutGroup? _grid;
		private RectTransform? _rect;
		private bool _applying;

		/// <summary>How many columns the grid is currently taking.</summary>
		public int Columns { get; private set; }

		private void OnEnable()
		{
			Theme.Changed += Apply;
			Apply();
		}

		private void OnDisable()
		{
			Theme.Changed -= Apply;
		}

		private void OnRectTransformDimensionsChange()
		{
			Apply();
		}

		private void OnValidate()
		{
			// Applying dirties the layout, which is not allowed from inside a validation callback.
			Deferred.Run(this, Apply);
		}

		/// <summary>Re-reads the grid's width and re-derives the column count and cell size.</summary>
		public void Apply()
		{
			// Writing the cell size dirties the layout, which can land back here in the same rebuild. Without
			// this the first resize would recurse until the stack gave out.
			if (_applying)
			{
				return;
			}

			_grid = _grid != null ? _grid : GetComponent<GridLayoutGroup>();
			_rect = _rect != null ? _rect : GetComponent<RectTransform>();

			if (_grid == null || _rect == null)
			{
				return;
			}

			float available = _rect.rect.width - _grid.padding.left - _grid.padding.right;

			// Zero-width happens for a frame while a screen is being laid out for the first time; deriving a
			// cell size from it would write a degenerate one and then have to be corrected.
			if (available <= 0f)
			{
				return;
			}

			float minimum = minCellWidth > 0f ? minCellWidth : Theme.Current.Layout.MinFeedCellWidth;
			int columns = minimum > 0f ? Mathf.FloorToInt(available / minimum) : minColumns;
			columns = Mathf.Max(minColumns, columns);

			float width = (available - (_grid.spacing.x * (columns - 1))) / columns;
			float height = cellAspect > 0f ? width * cellAspect : _grid.cellSize.y;

			Columns = columns;

			// Vector2 is forced here by the GridLayoutGroup cell-size API.
			var cell = new Vector2(width, height);

			bool unchanged = columns == _grid.constraintCount
				&& _grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
				&& Mathf.Approximately(cell.x, _grid.cellSize.x)
				&& Mathf.Approximately(cell.y, _grid.cellSize.y);

			if (unchanged)
			{
				return;
			}

			_applying = true;

			try
			{
				_grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
				_grid.constraintCount = columns;
				_grid.cellSize = cell;
			}
			finally
			{
				_applying = false;
			}
		}
	}
}
