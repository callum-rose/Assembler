using System.Collections.Generic;
using System.Linq;
using Assembler.Compiler.Compiler;
using Assembler.Libraries;
using UnityEngine;

namespace Spike.CompilerHarness.Cases.Adversarial
{
	/// <summary>
	/// Adversarial family 4 — <b>deep nesting and closure capture</b>. Every captured variable becomes a
	/// compiler-generated closure holding it, and every lambda becomes a delegate built over that closure
	/// at runtime. Nesting multiplies them: a lambda inside a loop inside a branch captures at several
	/// depths at once, and each distinct capture shape is another type IL2CPP has to have emitted ahead of
	/// time. Capturing a <i>struct</i> (the <c>Vector3</c> cases) is the sharpest version, for the same
	/// reason value-type generics are.
	///
	/// The nesting here is deliberately deeper than any real descriptor expression — the point is to find
	/// the depth at which something breaks, if one exists, not to model realistic usage.
	///
	/// <b>Shape constraint:</b> the compiler has no statement-body lambdas (<c>x =&gt; { … }</c>), so
	/// "nested control flow inside a lambda body" can only be expressed as nested ternaries. The
	/// statement-level nesting is exercised around the lambdas instead.
	/// </summary>
	public static class NestingAndClosureCases
	{
		private const string Suite = "Adversarial/NestingAndClosure";

		public static void Register(SpikeCaseList list)
		{
			list.Add($"{Suite}/LambdaCapturesOuterLocal", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<int>, int>(
					$$"""
					  int threshold = 2;
					  return numbers.Where(a => a > threshold).Count();
					  """,
					"numbers");

				Check.Equal(func(new List<int> { 1, 2, 3, 4, 5 }), 3);
			});

			list.Add($"{Suite}/LambdaCapturesMethodParameter", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<int>, int, int>(
					"return numbers.Where(a => a > threshold).Count();",
					"numbers",
					"threshold");

				Check.Equal(func(new List<int> { 1, 2, 3, 4, 5 }, 3), 2);
			});

			list.Add($"{Suite}/LambdaCapturesAtTwoNestingDepths", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<int>, int, int>(
					$$"""
					  int outerValue = 10;
					  int total = 0;
					  if (n > 0)
					  {
					      int middleValue = 20;
					      total = numbers.Select(a => a + outerValue + middleValue).Sum();
					  }
					  return total;
					  """,
					"numbers",
					"n");

				// (1+30) + (2+30) + (3+30) = 96.
				Check.Equal(func(new List<int> { 1, 2, 3 }, 1), 96);
			});

			list.Add($"{Suite}/LambdaCapturesLoopVariable", () =>
			{
				// A fresh closure per iteration over the loop variable — the capture shape most likely to
				// be miscompiled, and the one that yields a wrong answer rather than a crash when it is.
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<int>, int>(
					$$"""
					  int total = 0;
					  for (int i = 0; i < 3; i++)
					  {
					      total += numbers.Where(a => a > i).Count();
					  }
					  return total;
					  """,
					"numbers");

				// i=0 -> 3 kept, i=1 -> 2, i=2 -> 1. Total 6.
				Check.Equal(func(new List<int> { 1, 2, 3 }), 6);
			});

			list.Add($"{Suite}/ChainedLambdasEachCaptureADifferentLocal", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<int>, int>(
					$$"""
					  int lo = 1;
					  int hi = 4;
					  int scale = 3;
					  return numbers.Where(a => a > lo).Where(b => b < hi).Select(c => c * scale).Sum();
					  """,
					"numbers");

				// >1 -> {2,3,4,5}; <4 -> {2,3}; *3 -> {6,9} -> 15.
				Check.Equal(func(new List<int> { 1, 2, 3, 4, 5 }), 15);
			});

			list.Add($"{Suite}/LambdaCapturesStructLocal", () =>
			{
				// Capturing a Vector3 puts a struct in the closure — a distinct AOT specialisation from
				// capturing a reference or a primitive.
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<Vector3>, int>(
					$$"""
					  var origin = new Vector3(1f, 1f, 0f);
					  return points.Where(p => p.x > origin.x).Count();
					  """,
					"points");

				Check.Equal(func(SamplePoints()), 1);
			});

			list.Add($"{Suite}/NestedTernaryInsideLambdaBody", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<int>, int>(
					"return numbers.Select(a => a > 2 ? (a > 4 ? 100 : 10) : 1).Sum();",
					"numbers");

				// 1 -> 1, 2 -> 1, 3 -> 10, 4 -> 10, 5 -> 100 = 122.
				Check.Equal(func(new List<int> { 1, 2, 3, 4, 5 }), 122);
			});

			list.Add($"{Suite}/DeeplyNestedTernary", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, int>(
					"return n > 8 ? 4 : (n > 6 ? 3 : (n > 4 ? 2 : (n > 2 ? 1 : 0)));",
					"n");

				Check.Equal(func(9), 4);
				Check.Equal(func(7), 3);
				Check.Equal(func(5), 2);
				Check.Equal(func(3), 1);
				Check.Equal(func(1), 0);
			});

			list.Add($"{Suite}/LambdaCallsLocalMethodAndCapturesLocal", () =>
			{
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<int>, int>(
					$$"""
					  int offset = 5;
					  int bump(int v)
					  {
					      return v * 2;
					  }
					  return numbers.Select(a => bump(a) + offset).Sum();
					  """,
					"numbers");

				// (2+5) + (4+5) + (6+5) = 27.
				Check.Equal(func(new List<int> { 1, 2, 3 }), 27);
			});

			list.Add($"{Suite}/ChainedLocalMethodCalls", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, int>(
					$$"""
					  int addOne(int v)
					  {
					      return v + 1;
					  }
					  int doubled(int w)
					  {
					      return addOne(w) * 2;
					  }
					  int combined(int y)
					  {
					      return doubled(y) + addOne(y);
					  }
					  return combined(n);
					  """,
					"n");

				// addOne(3)=4, doubled(3)=8, combined(3)=8+4=12.
				Check.Equal(func(3), 12);
			});

			list.Add($"{Suite}/LambdaCallsRegisteredLibraryHelper", () =>
			{
				var compiler = NewLinqCompiler();
				compiler.RegisterStaticMethods(typeof(VectorMath));

				var func = compiler.CompileFunc<List<Vector3>, float>(
					"return points.Select(p => Magnitude(p)).Sum();",
					"points");

				var points = new List<Vector3> { new(3f, 4f, 0f), new(0f, 0f, 0f) };
				Check.Approx(func(points), 5f, 1e-4f);
			});

			list.Add($"{Suite}/DeeplyNestedControlFlow", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>(
					$$"""
					  int total = 0;
					  for (int i = 0; i < 4; i++)
					  {
					      if (i % 2 == 0)
					      {
					          int j = 0;
					          while (j < 3)
					          {
					              if (j == i)
					              {
					                  total += 100;
					              }
					              else
					              {
					                  total += 1;
					              }
					              j++;
					          }
					      }
					      else
					      {
					          total += 10;
					      }
					  }
					  return total;
					  """);

				// i=0 -> 100+1+1 = 102; i=1 -> 10; i=2 -> 1+1+100 = 102; i=3 -> 10. Total 224.
				Check.Equal(func(), 224);
			});

			list.Add($"{Suite}/LinqInsideDeeplyNestedControlFlowCapturingTwoLoopVariables", () =>
			{
				// The composite: a value-type LINQ chain in the innermost block of a four-level nest,
				// capturing both enclosing loop variables at once.
				var compiler = NewLinqCompiler();
				var func = compiler.CompileFunc<List<int>, int>(
					$$"""
					  int total = 0;
					  for (int i = 0; i < 3; i++)
					  {
					      if (i % 2 == 0)
					      {
					          int j = 0;
					          while (j < 2)
					          {
					              if (j == 1)
					              {
					                  total += numbers.Where(a => a > i).Select(b => b * j).Sum();
					              }
					              j++;
					          }
					      }
					  }
					  return total;
					  """,
					"numbers");

				// i=0, j=1 -> {1,2,3} scaled by 1 = 6; i=2, j=1 -> {3} scaled by 1 = 3. Total 9.
				Check.Equal(func(new List<int> { 1, 2, 3 }), 9);
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
