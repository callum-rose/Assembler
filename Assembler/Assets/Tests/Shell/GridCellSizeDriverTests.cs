using System.Collections.Generic;
using Assembler.Shell.Layout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.Shell
{
	/// <summary>
	/// The feed grid's column arithmetic: as many columns as fit at the theme's minimum cell width, never fewer
	/// than the floor, and cells that divide the width exactly.
	/// </summary>
	public class GridCellSizeDriverTests
	{
		private readonly List<GameObject> _created = new();

		[TearDown]
		public void TearDown()
		{
			foreach (var created in _created)
			{
				if (created != null)
				{
					Object.DestroyImmediate(created);
				}
			}

			_created.Clear();
		}

		[Test]
		public void TakesTwoColumnsOnAPhone()
		{
			var (driver, grid) = Grid(width: 390f, minCellWidth: 170f);

			Assert.AreEqual(2, driver.Columns);
			Assert.AreEqual(195f, grid.cellSize.x, 0.01f);
		}

		[Test]
		public void TakesMoreColumnsAsItGetsWider()
		{
			Assert.AreEqual(4, Grid(width: 700f, minCellWidth: 170f).Driver.Columns);
			Assert.AreEqual(6, Grid(width: 1024f, minCellWidth: 170f).Driver.Columns);
		}

		// Two columns is the floor, not the answer: a newspaper front page that ran one story wide would not
		// read as a front page.
		[Test]
		public void NeverDropsBelowTheMinimumColumnCount()
		{
			var (driver, grid) = Grid(width: 200f, minCellWidth: 170f);

			Assert.AreEqual(2, driver.Columns);
			Assert.AreEqual(100f, grid.cellSize.x, 0.01f);
		}

		[Test]
		public void TakesPaddingAndSpacingOutOfTheWidthFirst()
		{
			var (driver, grid) = Grid(width: 390f, minCellWidth: 170f, configure: g =>
			{
				g.padding = new RectOffset(20, 20, 0, 0);
				g.spacing = new Vector2(10f, 0f);
			});

			// 390 − 40 of padding − 10 of gutter, halved.
			Assert.AreEqual(2, driver.Columns);
			Assert.AreEqual(170f, grid.cellSize.x, 0.01f);
		}

		[Test]
		public void DerivesCellHeightFromTheAspectWhenOneIsGiven()
		{
			var (_, grid) = Grid(width: 390f, minCellWidth: 170f, aspect: 1.5f);

			Assert.AreEqual(grid.cellSize.x * 1.5f, grid.cellSize.y, 0.01f);
		}

		[Test]
		public void LeavesTheAuthoredHeightAloneWithoutAnAspect()
		{
			var (_, grid) = Grid(width: 390f, minCellWidth: 170f, configure: g =>
				g.cellSize = new Vector2(0f, 222f));

			Assert.AreEqual(222f, grid.cellSize.y, 0.01f);
		}

		private (GridCellSizeDriver Driver, GridLayoutGroup Grid) Grid(
			float width,
			float minCellWidth,
			float aspect = 0f,
			System.Action<GridLayoutGroup>? configure = null)
		{
			var target = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
			_created.Add(target);

			var rect = target.GetComponent<RectTransform>();
			rect.sizeDelta = new Vector2(width, 600f);

			var grid = target.GetComponent<GridLayoutGroup>();
			grid.spacing = Vector2.zero;
			configure?.Invoke(grid);

			var driver = target.AddComponent<GridCellSizeDriver>();
			var serialized = new UnityEditor.SerializedObject(driver);
			serialized.FindProperty("minCellWidth").floatValue = minCellWidth;
			serialized.FindProperty("cellAspect").floatValue = aspect;
			serialized.ApplyModifiedPropertiesWithoutUndo();

			driver.Apply();

			return (driver, grid);
		}
	}
}
