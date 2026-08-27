using System.Collections.Generic;
using Assembler.Compiler.Compiler;

namespace Spike.CompilerHarness.Cases
{
	/// <summary>
	/// Port of <c>Assets/Tests/Compiler/IndexerAndCollectionTests.cs</c> (25 cases): indexer /
	/// element-access and collection / dictionary initializers. Bodies are copied verbatim from the
	/// source suite. This suite matters disproportionately for AOT — every initializer and indexer path
	/// instantiates a generic collection type.
	/// </summary>
	public static class IndexerAndCollectionCases
	{
		private const string Suite = "IndexerAndCollection";

		public static void Register(SpikeCaseList list)
		{
			list.Add($"{Suite}/ListIndexerRead", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<List<int>, int>("return list[1];", "list");

				Check.Equal(func(new List<int> { 10, 20, 30 }), 20);
			});

			list.Add($"{Suite}/ListIndexerWithComputedIndex", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<List<int>, int>("return list[list.Count - 1];", "list");

				Check.Equal(func(new List<int> { 10, 20, 30 }), 30);
			});

			list.Add($"{Suite}/ListIndexerAssignment", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<List<int>, int>(
					$"""
					 list[0] = 99;
					 return list[0];
					 """,
					"list");

				Check.Equal(func(new List<int> { 1, 2, 3 }), 99);
			});

			list.Add($"{Suite}/ListIndexerCompoundAssignment", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var testList = new List<int> { 5, 6, 7 };
				var func = compiler.CompileFunc<List<int>, int>(
					$"""
					 list[1] += 10;
					 return list[1];
					 """,
					"list");

				Check.Equal(func(testList), 16);
			});

			list.Add($"{Suite}/ListIndexerIncrement", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<List<int>, int>(
					$"""
					 list[2]++;
					 return list[2];
					 """,
					"list");

				Check.Equal(func(new List<int> { 1, 2, 3 }), 4);
			});

			list.Add($"{Suite}/ArrayIndexerRead", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int[], int>("return arr[2];", "arr");

				Check.Equal(func(new[] { 100, 200, 300 }), 300);
			});

			list.Add($"{Suite}/ArrayIndexerAssignment", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var arr = new[] { 1, 2, 3 };
				var func = compiler.CompileFunc<int[], int>(
					$"""
					 arr[0] = arr[1] + arr[2];
					 return arr[0];
					 """,
					"arr");

				Check.Equal(func(arr), 5);
				Check.Equal(arr[0], 5, "arr[0] after invoke");
			});

			list.Add($"{Suite}/DictionaryIndexerRead", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(Dictionary<string, int>));
				var func = compiler.CompileFunc<Dictionary<string, int>, int>("return map[\"a\"];", "map");

				Check.Equal(func(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }), 1);
			});

			list.Add($"{Suite}/DictionaryIndexerAssignment", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var map = new Dictionary<string, int> { ["a"] = 1 };
				var func = compiler.CompileFunc<Dictionary<string, int>, int>(
					$"""
					 map["b"] = 7;
					 return map["b"];
					 """,
					"map");

				Check.Equal(func(map), 7);
				Check.Equal(map["b"], 7, "map[\"b\"] after invoke");
			});

			list.Add($"{Suite}/StringIndexerRead", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<string, char>("return s[0];", "s");

				Check.Equal(func("hello"), 'h');
			});

			list.Add($"{Suite}/IndexerInLoopSumsElements", () =>
			{
				var compiler = new ExpressionMethodCompiler();
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

				Check.Equal(func(new List<int> { 1, 2, 3, 4 }), 10);
			});

			list.Add($"{Suite}/MissingIndexerIsACompileError", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				Check.ThrowsCompile(() => compiler.CompileFunc<int, int>("return x[0];", "x"), "missing indexer");
			});

			list.Add($"{Suite}/ListInitializerWithInlineGeneric", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<List<int>>("return new List<int> { 1, 2, 3 };");

				Check.Sequence(func(), new List<int> { 1, 2, 3 });
			});

			list.Add($"{Suite}/ListInitializerWithEmptyParens", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<List<int>>("return new List<int>() { 10, 20 };");

				Check.Sequence(func(), new List<int> { 10, 20 });
			});

			list.Add($"{Suite}/ListInitializerWithComputedElements", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, List<int>>("return new List<int> { x, x * 2, x + 1 };", "x");

				Check.Sequence(func(5), new List<int> { 5, 10, 6 });
			});

			list.Add($"{Suite}/ListInitializerWithTrailingComma", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<List<int>>("return new List<int> { 1, 2, 3, };");

				Check.Sequence(func(), new List<int> { 1, 2, 3 });
			});

			list.Add($"{Suite}/EmptyListInitializer", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<List<int>>("return new List<int> { };");

				Check.IsEmpty(func());
			});

			list.Add($"{Suite}/ListInitializerThenIndexAndCount", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>(
					$$"""
					var nums = new List<int> { 4, 5, 6 };
					return nums[0] + nums[nums.Count - 1];
					""");

				Check.Equal(func(), 10);
			});

			list.Add($"{Suite}/StringListInitializer", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<List<string>>("return new List<string> { \"a\", \"b\" };");

				Check.Sequence(func(), new List<string> { "a", "b" });
			});

			list.Add($"{Suite}/DictionaryInitializer", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<Dictionary<string, int>>(
					"return new Dictionary<string, int> { { \"a\", 1 }, { \"b\", 2 } };");

				var result = func();
				Check.Equal(result["a"], 1, "result[\"a\"]");
				Check.Equal(result["b"], 2, "result[\"b\"]");
			});

			list.Add($"{Suite}/DictionaryInitializerWithComputedValues", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, Dictionary<int, int>>(
					"return new Dictionary<int, int> { { 1, n }, { 2, n * n } };", "n");

				var result = func(4);
				Check.Equal(result[1], 4, "result[1]");
				Check.Equal(result[2], 16, "result[2]");
			});

			list.Add($"{Suite}/NestedGenericListInitializer", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<Dictionary<string, List<int>>>(
					"return new Dictionary<string, List<int>> { };");

				Check.IsEmpty(func());
			});

			list.Add($"{Suite}/ListInitializerFeedsLinq", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterStaticMethods(typeof(System.Linq.Enumerable));
				var func = compiler.CompileFunc<int>("return new List<int> { 1, 2, 3, 4 }.Where(x => x > 2).Sum();");

				Check.Equal(func(), 7);
			});

			list.Add($"{Suite}/RegisteredAliasCollectionInitializer", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(List<int>), "IntList");
				var func = compiler.CompileFunc<List<int>>("return new IntList { 7, 8 };");

				Check.Sequence(func(), new List<int> { 7, 8 });
			});

			list.Add($"{Suite}/InitializerOnTypeWithoutAddIsACompileError", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(TestVector3), "TestVector3");

				Check.ThrowsCompile(
					() => compiler.CompileFunc<TestVector3>("return new TestVector3(1, 2, 3) { 4 };"),
					"initializer on a type without Add");
			});
		}
	}
}
