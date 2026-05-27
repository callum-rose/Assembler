using System.Collections.Generic;
using Assembler.Behaviours.ListOperations;
using Assembler.Parsing;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Parsing
{
	public class ListFillBehaviourTests
	{
		[Test]
		public void Fill_OnEmptyList_AddsCountCopiesOfValue()
		{
			var list = new List<int>();

			ListFillBehaviour<int>.Fill(list, 5, 42);

			Assert.AreEqual(5, list.Count);
			CollectionAssert.AreEqual(new[] { 42, 42, 42, 42, 42 }, list);
		}

		[Test]
		public void Fill_OnNonEmptyList_ReplacesExistingContents()
		{
			var list = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };

			ListFillBehaviour<int>.Fill(list, 3, 0);

			Assert.AreEqual(3, list.Count);
			CollectionAssert.AreEqual(new[] { 0, 0, 0 }, list);
		}

		[Test]
		public void Fill_WithZeroCount_ClearsList()
		{
			var list = new List<string> { "a", "b", "c" };

			ListFillBehaviour<string>.Fill(list, 0, "x");

			Assert.AreEqual(0, list.Count);
		}

		[Test]
		public void Fill_WithNegativeCount_ClearsList()
		{
			var list = new List<float> { 1f, 2f };

			ListFillBehaviour<float>.Fill(list, -10, 9f);

			Assert.AreEqual(0, list.Count);
		}

		[Test]
		public void Fill_WithLargeCount_ProducesExpectedSize()
		{
			var list = new List<bool>();

			ListFillBehaviour<bool>.Fill(list, 200, true);

			Assert.AreEqual(200, list.Count);
			foreach (var item in list)
			{
				Assert.IsTrue(item);
			}
		}

		[Test]
		public void Fill_WorksForVectorType()
		{
			var list = new List<Vector3>();

			ListFillBehaviour<Vector3>.Fill(list, 3, new Vector3(1, 2, 3));

			Assert.AreEqual(3, list.Count);
			foreach (var v in list)
			{
				Assert.AreEqual(new Vector3(1, 2, 3), v);
			}
		}

		[Test]
		public void Fill_WorksForColourType()
		{
			var list = new List<Color> { Color.black };

			ListFillBehaviour<Color>.Fill(list, 4, Color.red);

			Assert.AreEqual(4, list.Count);
			foreach (var c in list)
			{
				Assert.AreEqual(Color.red, c);
			}
		}

		[TestCase("vector list fill")]
		[TestCase("int list fill")]
		[TestCase("float list fill")]
		[TestCase("bool list fill")]
		[TestCase("string list fill")]
		[TestCase("colour list fill")]
		public void BehaviourRegistry_ContainsListFillForEveryElementType(string key)
		{
			Assert.IsTrue(BehaviourRegistry.All.ContainsKey(key),
				$"Expected BehaviourRegistry to contain '{key}'");
		}

		[Test]
		public void ListFillInfo_IsGenericRecordWithCountAndValue()
		{
			// Smoke check that the Info type exists and exposes Count and Value.
			var infoType = typeof(ListFillInfo<int>);
			Assert.NotNull(infoType.GetProperty("Count"), "ListFillInfo<T> should expose Count");
			Assert.NotNull(infoType.GetProperty("Value"), "ListFillInfo<T> should expose Value");
			Assert.NotNull(infoType.GetProperty("List"), "ListFillInfo<T> should expose List");
		}
	}
}
