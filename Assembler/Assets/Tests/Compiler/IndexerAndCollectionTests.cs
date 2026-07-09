using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Compiler.Compiler;
using NUnit.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tests.Compiler
{
	/// <summary>
	/// Indexer / element-access and collection / dictionary initializer tests.
	/// </summary>
	public class IndexerAndCollectionTests : CompilerTestBase
	{
		// --- Indexer / element access ---

		[Test]
		public void ListIndexerRead()
		{
			var func = compiler.CompileFunc<List<int>, int>("return list[1];", "list");

			Assert.That(func(new List<int> { 10, 20, 30 }), Is.EqualTo(20));
		}

		[Test]
		public void ListIndexerWithComputedIndex()
		{
			var func = compiler.CompileFunc<List<int>, int>("return list[list.Count - 1];", "list");

			Assert.That(func(new List<int> { 10, 20, 30 }), Is.EqualTo(30));
		}

		[Test]
		public void ListIndexerAssignment()
		{
			var func = compiler.CompileFunc<List<int>, int>(
				$"""
				 list[0] = 99;
				 return list[0];
				 """,
				"list");

			Assert.That(func(new List<int> { 1, 2, 3 }), Is.EqualTo(99));
		}

		[Test]
		public void ListIndexerCompoundAssignment()
		{
			var testList = new List<int> { 5, 6, 7 };
			var func = compiler.CompileFunc<List<int>, int>(
				$"""
				 list[1] += 10;
				 return list[1];
				 """,
				"list");

			Assert.That(func(testList), Is.EqualTo(16));
		}

		[Test]
		public void ListIndexerIncrement()
		{
			var func = compiler.CompileFunc<List<int>, int>(
				$"""
				 list[2]++;
				 return list[2];
				 """,
				"list");

			Assert.That(func(new List<int> { 1, 2, 3 }), Is.EqualTo(4));
		}

		[Test]
		public void ArrayIndexerRead()
		{
			var func = compiler.CompileFunc<int[], int>("return arr[2];", "arr");

			Assert.That(func(new[] { 100, 200, 300 }), Is.EqualTo(300));
		}

		[Test]
		public void ArrayIndexerAssignment()
		{
			var arr = new[] { 1, 2, 3 };
			var func = compiler.CompileFunc<int[], int>(
				$"""
				 arr[0] = arr[1] + arr[2];
				 return arr[0];
				 """,
				"arr");

			Assert.That(func(arr), Is.EqualTo(5));
			Assert.That(arr[0], Is.EqualTo(5));
		}

		[Test]
		public void DictionaryIndexerRead()
		{
			compiler.RegisterType(typeof(Dictionary<string, int>));
			var func = compiler.CompileFunc<Dictionary<string, int>, int>("return map[\"a\"];", "map");

			Assert.That(func(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }), Is.EqualTo(1));
		}

		[Test]
		public void DictionaryIndexerAssignment()
		{
			var map = new Dictionary<string, int> { ["a"] = 1 };
			var func = compiler.CompileFunc<Dictionary<string, int>, int>(
				$"""
				 map["b"] = 7;
				 return map["b"];
				 """,
				"map");

			Assert.That(func(map), Is.EqualTo(7));
			Assert.That(map["b"], Is.EqualTo(7));
		}

		[Test]
		public void StringIndexerRead()
		{
			var func = compiler.CompileFunc<string, char>("return s[0];", "s");

			Assert.That(func("hello"), Is.EqualTo('h'));
		}

		[Test]
		public void IndexerInLoopSumsElements()
		{
			var func = compiler.CompileFunc<List<int>, int>(
				$$"""
				int total = 0;
				for (int i = 0; i < list.Count; i++)
				{
				    total += list[i];
				}
				return total;
				""",
				"list");

			Assert.That(func(new List<int> { 1, 2, 3, 4 }), Is.EqualTo(10));
		}

		[Test]
		public void MissingIndexerIsACompileError()
		{

			Assert.Throws<CompileException>(() => compiler.CompileFunc<int, int>("return x[0];", "x"));
		}

		// --- Collection / dictionary initializers ---

		[Test]
		public void ListInitializerWithInlineGeneric()
		{
			var func = compiler.CompileFunc<List<int>>("return new List<int> { 1, 2, 3 };");

			Assert.That(func(), Is.EqualTo(new List<int> { 1, 2, 3 }));
		}

		[Test]
		public void ListInitializerWithEmptyParens()
		{
			var func = compiler.CompileFunc<List<int>>("return new List<int>() { 10, 20 };");

			Assert.That(func(), Is.EqualTo(new List<int> { 10, 20 }));
		}

		[Test]
		public void ListInitializerWithComputedElements()
		{
			var func = compiler.CompileFunc<int, List<int>>("return new List<int> { x, x * 2, x + 1 };", "x");

			Assert.That(func(5), Is.EqualTo(new List<int> { 5, 10, 6 }));
		}

		[Test]
		public void ListInitializerWithTrailingComma()
		{
			var func = compiler.CompileFunc<List<int>>("return new List<int> { 1, 2, 3, };");

			Assert.That(func(), Is.EqualTo(new List<int> { 1, 2, 3 }));
		}

		[Test]
		public void EmptyListInitializer()
		{
			var func = compiler.CompileFunc<List<int>>("return new List<int> { };");

			Assert.That(func(), Is.Empty);
		}

		[Test]
		public void ListInitializerThenIndexAndCount()
		{
			var func = compiler.CompileFunc<int>(
				$$"""
				var nums = new List<int> { 4, 5, 6 };
				return nums[0] + nums[nums.Count - 1];
				""");

			Assert.That(func(), Is.EqualTo(10));
		}

		[Test]
		public void StringListInitializer()
		{
			var func = compiler.CompileFunc<List<string>>("return new List<string> { \"a\", \"b\" };");

			Assert.That(func(), Is.EqualTo(new List<string> { "a", "b" }));
		}

		[Test]
		public void DictionaryInitializer()
		{
			var func = compiler.CompileFunc<Dictionary<string, int>>(
				"return new Dictionary<string, int> { { \"a\", 1 }, { \"b\", 2 } };");

			var result = func();
			Assert.That(result["a"], Is.EqualTo(1));
			Assert.That(result["b"], Is.EqualTo(2));
		}

		[Test]
		public void DictionaryInitializerWithComputedValues()
		{
			var func = compiler.CompileFunc<int, Dictionary<int, int>>(
				"return new Dictionary<int, int> { { 1, n }, { 2, n * n } };", "n");

			var result = func(4);
			Assert.That(result[1], Is.EqualTo(4));
			Assert.That(result[2], Is.EqualTo(16));
		}

		[Test]
		public void NestedGenericListInitializer()
		{
			var func = compiler.CompileFunc<Dictionary<string, List<int>>>(
				"return new Dictionary<string, List<int>> { };");

			Assert.That(func(), Is.Empty);
		}

		[Test]
		public void ListInitializerFeedsLinq()
		{
			compiler.RegisterStaticMethods(typeof(System.Linq.Enumerable));
			var func = compiler.CompileFunc<int>("return new List<int> { 1, 2, 3, 4 }.Where(x => x > 2).Sum();");

			Assert.That(func(), Is.EqualTo(7));
		}

		[Test]
		public void RegisteredAliasCollectionInitializer()
		{
			compiler.RegisterType(typeof(List<int>), "IntList");
			var func = compiler.CompileFunc<List<int>>("return new IntList { 7, 8 };");

			Assert.That(func(), Is.EqualTo(new List<int> { 7, 8 }));
		}

		[Test]
		public void InitializerOnTypeWithoutAddIsACompileError()
		{
			compiler.RegisterType(typeof(TestVector3), "TestVector3");

			Assert.Throws<CompileException>(
				() => compiler.CompileFunc<TestVector3>("return new TestVector3(1, 2, 3) { 4 };"));
		}
	}
}
