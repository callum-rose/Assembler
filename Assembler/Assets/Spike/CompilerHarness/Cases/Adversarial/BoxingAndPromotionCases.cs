using System.Collections.Generic;
using System.Linq;
using Assembler.Compiler.Compiler;
using UnityEngine;

namespace Spike.CompilerHarness.Cases.Adversarial
{
	/// <summary>
	/// Adversarial family 3 — <b>boxing and numeric promotion</b>. Unlike the other three families, most
	/// of these cases are looking for a <i>wrong answer</i> rather than a crash. A stripped or mis-shared
	/// generic can make a conversion silently pick the wrong width — <c>int</c> arithmetic where
	/// <c>float</c> was meant, a truncation that should have been a widening — and that produces a game
	/// that runs and misbehaves, which is far worse to diagnose on-device than a clean
	/// <c>ExecutionEngineException</c>. So every case here asserts the value, not just that it returned.
	///
	/// <b>Coverage note.</b> The spec names <c>BoxingValueProvider</c>. That type lives in
	/// <c>Assembler.Resolving</c> and adapts an <c>IValueProvider</c> to <c>IValueProvider&lt;object&gt;</c>;
	/// it is only reachable through a <i>resolved descriptor value</i>, never from the raw compiler. The
	/// cases here cover the compiler-level boxing conversion (a value type flowing into an
	/// <c>object</c>-typed site, which is the same CLR operation); the provider wrapper itself is the
	/// descriptor half's job.
	/// </summary>
	public static class BoxingAndPromotionCases
	{
		private const string Suite = "Adversarial/BoxingAndPromotion";

		public static void Register(SpikeCaseList list)
		{
			list.Add($"{Suite}/BoxIntIntoObjectReturn", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<object>("return 42;");

				Check.Equal(func(), 42);
			});

			list.Add($"{Suite}/BoxFloatIntoObjectReturn", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<object>("return 1.5f;");

				Check.Equal(func(), 1.5f);
			});

			list.Add($"{Suite}/BoxBoolIntoObjectReturn", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<object>("return true;");

				Check.Equal(func(), true);
			});

			list.Add($"{Suite}/BoxStructIntoObjectReturn", () =>
			{
				// Boxing a Vector3: the struct case, which needs its own AOT specialisation in a way the
				// primitive boxes do not.
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(Vector3));
				var func = compiler.CompileFunc<object>("return new Vector3(1f, 2f, 3f);");

				Check.Equal(func(), new Vector3(1f, 2f, 3f));
			});

			list.Add($"{Suite}/BoxParameterisedStructIntoObjectReturn", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(Vector3));
				var func = compiler.CompileFunc<Vector3, object>("return v * 2f;", "v");

				Check.Equal(func(new Vector3(1f, 2f, 3f)), new Vector3(2f, 4f, 6f));
			});

			list.Add($"{Suite}/ThreeWayPromotionChain", () =>
			{
				// Three parameters, so this goes through Compile directly — CompileFunc<> tops out at two.
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.Compile(
					$$"""
					  float widened = i;
					  double promoted = widened + f;
					  return promoted + d;
					  """,
					typeof(double),
					out _,
					(typeof(int), "i"),
					(typeof(float), "f"),
					(typeof(double), "d"));

				// 3 + 0.5 + 0.25 = 3.75, computed entirely through widening conversions.
				var result = func.DynamicInvoke(3, 0.5f, 0.25);
				Check.Approx((double)result!, 3.75, 1e-9);
			});

			list.Add($"{Suite}/DoubleNarrowedBackToIntTruncatesTowardZero", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<double, int>("return (int)d;", "d");

				Check.Equal(func(2.9), 2);
				Check.Equal(func(-2.9), -2);
			});

			list.Add($"{Suite}/CompoundAssignNarrowsFloatIntoInt", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<float, int>(
					$$"""
					  int total = 0;
					  total += f;
					  total += f;
					  return total;
					  """,
					"f");

				// Each += truncates independently: 2 + 2, not (int)(2.7 + 2.7).
				Check.Equal(func(2.7f), 4);
			});

			list.Add($"{Suite}/MixedTypeComparisonsAcrossAllOperators", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, float, bool>(
					$$"""
					  bool a = i < f;
					  bool b = i <= f;
					  bool c = i > f;
					  bool d = i >= f;
					  bool e = i == f;
					  bool g = i != f;
					  return a && b && !c && !d && !e && g;
					  """,
					"i",
					"f");

				// 2 vs 2.5: less-than and less-or-equal hold, the rest do not, and they are not equal.
				Check.True(func(2, 2.5f), "mixed int/float comparisons");
			});

			list.Add($"{Suite}/IntEqualsFloatExactly", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, float, bool>("return i == f;", "i", "f");

				Check.True(func(2, 2f), "2 == 2f");
				Check.False(func(2, 2.5f), "2 == 2.5f");
			});

			list.Add($"{Suite}/TernaryUnifiesIntAndFloatToFloat", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<bool, float>("return c ? 1 : 2.5f;", "c");

				Check.Approx(func(true), 1f, 1e-6f);
				Check.Approx(func(false), 2.5f, 1e-6f);
			});

			list.Add($"{Suite}/IntegerDivisionStaysIntegral", () =>
			{
				// The classic wrong-answer trap: if promotion fires too eagerly this returns 3.5, not 3.
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>("return 7 / 2;");

				Check.Equal(func(), 3);
			});

			list.Add($"{Suite}/IntegerDivisionPromotedByAFloatOperand", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<float>("return 7 / 2f;");

				Check.Approx(func(), 3.5f, 1e-6f);
			});

			list.Add($"{Suite}/PromotionInsideLinqSelector", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterStaticMethods(typeof(Enumerable));
				var func = compiler.CompileFunc<List<int>, double>(
					"return numbers.Select(n => n * 1.5).Sum();",
					"numbers");

				// (1 + 2 + 3) * 1.5 = 9.0, with the selector promoting int to double per element.
				Check.Approx(func(new List<int> { 1, 2, 3 }), 9.0, 1e-9);
			});

			list.Add($"{Suite}/VectorScaledByPromotedIntExpression", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(Vector3));
				var func = compiler.CompileFunc<Vector3, int, Vector3>("return v * (n + 1);", "v", "n");

				Check.Equal(func(new Vector3(1f, 2f, 3f), 2), new Vector3(3f, 6f, 9f));
			});

			list.Add($"{Suite}/StructFieldWriteCoercesIntToFloat", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(CoercionTarget), "CoercionTarget");
				var func = compiler.CompileFunc<int, float>(
					$$"""
					  CoercionTarget t = new CoercionTarget();
					  t.Value = n;
					  return t.Value;
					  """,
					"n");

				Check.Approx(func(7), 7f, 1e-6f);
			});
		}
	}
}
