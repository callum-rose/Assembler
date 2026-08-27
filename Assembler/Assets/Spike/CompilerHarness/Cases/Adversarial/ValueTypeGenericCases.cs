using System.Collections.Generic;
using System.Linq;
using Assembler.Compiler.Compiler;
using UnityEngine;

namespace Spike.CompilerHarness.Cases.Adversarial
{
	/// <summary>
	/// Adversarial family 1 — <b>value-type generic instantiation</b>. Highest priority: this is the
	/// classic IL2CPP <c>ExecutionEngineException</c> source. A generic method or comparer instantiated
	/// over a <i>struct</i> needs its own specialised native code; the AOT compiler only emits what it can
	/// see statically, and a chain assembled at runtime from an expression tree is precisely what it
	/// cannot see. Reference-type instantiations share one canonical implementation and so almost never
	/// fail this way, which is why every case here is keyed on <c>Vector3</c>, <c>int</c> or <c>float</c>.
	///
	/// The heaviest offenders are the ones that pull in an implicit comparer:
	/// <c>OrderBy</c> (<c>Comparer&lt;TKey&gt;.Default</c>), <c>Distinct</c>/<c>GroupBy</c>
	/// (<c>EqualityComparer&lt;T&gt;.Default</c> plus an internal <c>Set&lt;T&gt;</c>/<c>Lookup&lt;K,V&gt;</c>).
	///
	/// <b>Not covered — two compiler limitations, neither an AOT finding:</b>
	/// <list type="bullet">
	/// <item><c>Aggregate</c>, named in the spec. Its useful overloads take a two-parameter lambda and the
	/// compiler supports single-parameter lambdas only (COMPILER_SYNTAX_REFERENCE.md, "Not Supported -
	/// Lambdas"), so no valid expression string reaches it.</item>
	/// <item><c>SelectMany</c>. <c>groups.SelectMany(g =&gt; g)</c> fails to compile: the lambda's return
	/// type infers as <c>IEnumerable&lt;object&gt;</c> rather than <c>IEnumerable&lt;int&gt;</c>, giving
	/// <i>"Expression of type List&lt;Int32&gt; cannot be used for return type IEnumerable&lt;Object&gt;"</i>.
	/// Nested-collection flattening is covered by <c>NestedGenericListFlattenedByIndexer</c> instead,
	/// which reaches the same <c>List&lt;List&lt;int&gt;&gt;</c> instantiations through an indexer.</item>
	/// </list>
	/// </summary>
	public static class ValueTypeGenericCases
	{
		private const string Suite = "Adversarial/ValueTypeGeneric";

		public static void Register(SpikeCaseList list)
		{
			list.Add($"{Suite}/WhereOverStructList", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<Vector3>, int>(
					"return points.Where(p => p.x > 0f).Count();",
					"points");

				Check.Equal(func(SamplePoints()), 2);
			});

			list.Add($"{Suite}/SelectStructToFloatThenSum", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<Vector3>, float>(
					"return points.Select(p => p.y).Sum();",
					"points");

				// y components: 1 + 2 + 3 + 4 = 10.
				Check.Approx(func(SamplePoints()), 10f, 1e-4f);
			});

			list.Add($"{Suite}/SelectStructToStruct", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<Vector3>, Vector3>(
					"return points.Select(p => p * 2f).First();",
					"points");

				Check.Equal(func(SamplePoints()), new Vector3(-2f, 2f, 0f));
			});

			list.Add($"{Suite}/WhereSelectChainOverStructList", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<Vector3>, float>(
					"return points.Where(p => p.x > 0f).Select(p => p.y).Sum();",
					"points");

				// x > 0 keeps y = 2 and y = 3.
				Check.Approx(func(SamplePoints()), 5f, 1e-4f);
			});

			list.Add($"{Suite}/OrderByStructKeyedByFloat", () =>
			{
				// Comparer<float>.Default over an OrderedEnumerable<Vector3, float> — the single densest
				// value-type generic instantiation in the whole corpus.
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<Vector3>, Vector3>(
					"return points.OrderBy(p => p.x).First();",
					"points");

				Check.Equal(func(SamplePoints()), new Vector3(-1f, 1f, 0f));
			});

			list.Add($"{Suite}/OrderByDescendingIntList", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<int>, int>(
					"return numbers.OrderByDescending(n => n).First();",
					"numbers");

				Check.Equal(func(new List<int> { 3, 9, 1, 7 }), 9);
			});

			list.Add($"{Suite}/OrderByThenSelectToList", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<Vector3>, List<float>>(
					"return points.OrderBy(p => p.x).Select(p => p.x).ToList();",
					"points");

				Check.Sequence(func(SamplePoints()), new List<float> { -1f, 0f, 1f, 2f });
			});

			list.Add($"{Suite}/DistinctOverIntList", () =>
			{
				// EqualityComparer<int>.Default plus LINQ's internal Set<int>.
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<int>, int>(
					"return numbers.Distinct().Count();",
					"numbers");

				Check.Equal(func(new List<int> { 1, 2, 2, 3, 3, 3 }), 3);
			});

			list.Add($"{Suite}/GroupByIntKey", () =>
			{
				// Lookup<int, int> — the heaviest generic machinery LINQ has that this compiler can reach.
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<int>, int>(
					"return numbers.GroupBy(n => n % 3).Count();",
					"numbers");

				Check.Equal(func(new List<int> { 1, 2, 3, 4, 5, 6 }), 3);
			});

			list.Add($"{Suite}/MinMaxOverFloatList", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<float>, float>(
					"return values.Max() - values.Min();",
					"values");

				Check.Approx(func(new List<float> { 2.5f, -1f, 7f }), 8f, 1e-4f);
			});

			list.Add($"{Suite}/AnyAllOverStructList", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<Vector3>, bool>(
					"return points.Any(p => p.x > 1f) && points.All(p => p.z == 0f);",
					"points");

				Check.True(func(SamplePoints()), "any/all over List<Vector3>");
			});

			list.Add($"{Suite}/TakeSkipReverseOverStructList", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<Vector3>, float>(
					"return points.Skip(1).Take(2).Reverse().Select(p => p.y).First();",
					"points");

				// Skip(1) -> y {2,3,4}; Take(2) -> {2,3}; Reverse -> {3,2}; First -> 3.
				Check.Approx(func(SamplePoints()), 3f, 1e-4f);
			});

			list.Add($"{Suite}/NestedGenericListFlattenedByIndexer", () =>
			{
				// Nested-collection flattening, reached by indexer rather than SelectMany (see the class
				// note). Still instantiates List<List<int>> and runs a LINQ chain over each inner List<int>.
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<List<int>>, int>(
					$$"""
					  int total = 0;
					  for (int i = 0; i < groups.Count; i++)
					  {
					      total += groups[i].Where(a => a > 0).Sum();
					  }
					  return total;
					  """,
					"groups");

				var groups = new List<List<int>>
				{
					new() { 1, 2 },
					new() { 3, 4 }
				};

				Check.Equal(func(groups), 10);
			});

			list.Add($"{Suite}/SumWithStructSelector", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<Vector3>, float>(
					"return points.Sum(p => p.x + p.y);",
					"points");

				// (-1+1) + (1+2) + (2+3) + (0+4) = 0 + 3 + 5 + 4 = 12.
				Check.Approx(func(SamplePoints()), 12f, 1e-4f);
			});

			list.Add($"{Suite}/DictionaryWithStructValues", () =>
			{
				// Dictionary<int, Vector3>: a generic dictionary keyed by one value type and storing another.
				// Deliberately NOT registered: RegisterType would bind the name "Dictionary" to this *closed*
				// type, and the inline-generic construction path would then try to MakeGenericType on it and
				// fail. The built-in System.Collections.Generic resolution handles it unregistered.
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<Vector3>(
					$$"""
					  var map = new Dictionary<int, Vector3> { };
					  map[1] = new UnityEngine.Vector3(1f, 2f, 3f);
					  map[2] = new UnityEngine.Vector3(4f, 5f, 6f);
					  return map[1] + map[2];
					  """);

				Check.Equal(func(), new Vector3(5f, 7f, 9f));
			});

			list.Add($"{Suite}/StructListInitializerWithComputedElements", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<float, List<Vector3>>(
					"return new List<Vector3> { new Vector3(s, 0f, 0f), new Vector3(0f, s, 0f) };",
					"s");

				Check.Sequence(
					func(3f),
					new List<Vector3> { new(3f, 0f, 0f), new(0f, 3f, 0f) });
			});

			list.Add($"{Suite}/StructListBuiltInLoopThenOrderedAndSummed", () =>
			{
				// The full pipeline in one expression: construct a List<Vector3> imperatively, then run an
				// ordered value-type LINQ chain over it. If any single stage lacks its AOT specialisation,
				// this is where a device build falls over.
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<int, float>(
					$$"""
					  var points = new List<Vector3> { };
					  for (int i = 0; i < n; i++)
					  {
					      points.Add(new Vector3(n - i, i * 2f, 0f));
					  }
					  return points.OrderBy(p => p.x).Where(p => p.y > 0f).Select(p => p.x + p.y).Sum();
					  """,
					"n");

				// n = 4 -> points (x, y): (4,0) (3,2) (2,4) (1,6); y > 0 keeps three; sums of x+y: 7, 6, 5 = 18.
				Check.Approx(func(4), 18f, 1e-4f);
			});
		}

		private static ExpressionMethodCompiler NewLinqCompiler()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterStaticMethods(typeof(Enumerable));
			compiler.RegisterType(typeof(Vector3));
			return compiler;
		}

		private static List<Vector3> SamplePoints() => new()
		{
			new Vector3(-1f, 1f, 0f),
			new Vector3(1f, 2f, 0f),
			new Vector3(2f, 3f, 0f),
			new Vector3(0f, 4f, 0f)
		};
	}
}
