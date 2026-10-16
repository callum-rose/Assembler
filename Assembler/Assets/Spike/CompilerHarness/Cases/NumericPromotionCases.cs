using Assembler.Compiler.Compiler;

namespace Spike.CompilerHarness.Cases
{
	/// <summary>
	/// Port of <c>Assets/Tests/Compiler/NumericPromotionTests.cs</c> (29 cases): implicit numeric
	/// promotion, explicit casts, float-literal arithmetic, string-escape interpretation and numeric
	/// coercion at assignment-shaped sites. Bodies are copied verbatim from the source suite.
	/// </summary>
	public static class NumericPromotionCases
	{
		private const string Suite = "NumericPromotion";

		public static void Register(SpikeCaseList list)
		{
			list.Add($"{Suite}/FloatLiteralArithmetic", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<float>("return 1.5f + 2.5f;");
				Check.Approx(func(), 4f, 0.0001f);
			});

			list.Add($"{Suite}/CastToDouble", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<double>("return (double)7 / (double)2;");
				Check.Equal(func(), 3.5);
			});

			list.Add($"{Suite}/CastToFloat", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<float>("return (float)9 / (float)2;");
				Check.Approx(func(), 4.5f, 0.0001f);
			});

			list.Add($"{Suite}/StringEscapesAreInterpreted", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<string>("return \"a\\nb\";");
				Check.Equal(func(), "a\nb");
			});

			list.Add($"{Suite}/FloatPlusIntPromotes", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<float, int, float>("return x + y;", "x", "y");
				Check.Approx(func(1.5f, 2), 3.5f, 0.0001f);
			});

			list.Add($"{Suite}/IntPlusFloatPromotes", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, float, float>("return x + y;", "x", "y");
				Check.Approx(func(2, 1.5f), 3.5f, 0.0001f);
			});

			list.Add($"{Suite}/IntMinusFloatPromotes", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, float, float>("return x - y;", "x", "y");
				Check.Approx(func(5, 1.5f), 3.5f, 0.0001f);
			});

			list.Add($"{Suite}/IntTimesFloatPromotes", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, float, float>("return x * y;", "x", "y");
				Check.Approx(func(3, 2.5f), 7.5f, 0.0001f);
			});

			list.Add($"{Suite}/IntDividedByFloatPromotes", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, float, float>("return x / y;", "x", "y");
				Check.Approx(func(5, 2f), 2.5f, 0.0001f);
			});

			list.Add($"{Suite}/FloatModuloIntPromotes", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<float, int, float>("return x % y;", "x", "y");
				Check.Approx(func(5.5f, 2), 1.5f, 0.0001f);
			});

			list.Add($"{Suite}/IntPlusDoublePromotes", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, double, double>("return x + y;", "x", "y");
				Check.Approx(func(2, 1.5), 3.5, 0.0001);
			});

			list.Add($"{Suite}/MixedLessThanComparison", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<float, int, bool>("return x < y;", "x", "y");
				Check.True(func(1.5f, 2), "func(1.5f, 2)");
				Check.False(func(2.5f, 2), "func(2.5f, 2)");
			});

			list.Add($"{Suite}/MixedGreaterThanOrEqualComparison", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, float, bool>("return x >= y;", "x", "y");
				Check.True(func(3, 2.5f), "func(3, 2.5f)");
				Check.False(func(2, 2.5f), "func(2, 2.5f)");
			});

			list.Add($"{Suite}/MixedEqualityComparison", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<float, int, bool>("return x == y;", "x", "y");
				Check.True(func(2f, 2), "func(2f, 2)");
				Check.False(func(2.5f, 2), "func(2.5f, 2)");
			});

			list.Add($"{Suite}/FloatVariablePlusEqualsInt", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, float>(
					$$"""
					float total = 1.5f;
					total += x;
					return total;
					""",
					"x");
				Check.Approx(func(2), 3.5f, 0.0001f);
			});

			list.Add($"{Suite}/FloatVariableMinusEqualsInt", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, float>(
					$$"""
					float total = 5f;
					total -= x;
					return total;
					""",
					"x");
				Check.Approx(func(2), 3f, 0.0001f);
			});

			list.Add($"{Suite}/FloatVariableTimesEqualsInt", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, float>(
					$$"""
					float total = 2.5f;
					total *= x;
					return total;
					""",
					"x");
				Check.Approx(func(3), 7.5f, 0.0001f);
			});

			list.Add($"{Suite}/IntVariablePlusEqualsFloatNarrowsBack", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<float, int>(
					$$"""
					int total = 5;
					total += x;
					return total;
					""",
					"x");
				Check.Equal(func(2.9f), 7);
			});

			list.Add($"{Suite}/ReturnCoercesIntLiteralToFloat", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<float>("return 1;");
				Check.Equal(func(), 1f);
			});

			list.Add($"{Suite}/ReturnCoercesDoubleLiteralToFloat", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<float>("return 0.5;");
				Check.Equal(func(), 0.5f);
			});

			list.Add($"{Suite}/ImplicitReturnCoercesToReturnType", () =>
			{
				var compiler = new ExpressionMethodCompiler();

				// No explicit `return` — the trailing expression statement is the implicit return value.
				var func = compiler.CompileFunc<float>("1;");
				Check.Equal(func(), 1f);
			});

			list.Add($"{Suite}/PlainAssignCoercesToVariableType", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<float>("float x = 0f; x = 1; return x;");
				Check.Equal(func(), 1f);
			});

			list.Add($"{Suite}/DeclarationCoercesInitializer", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<float>("float x = 1; return x;");
				Check.Equal(func(), 1f);
			});

			list.Add($"{Suite}/TernaryUnifiesNumericBranches", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<bool, double>("return c ? 1 : 2.0;", "c");
				Check.Equal(func(true), 1.0);
				Check.Equal(func(false), 2.0);
			});

			list.Add($"{Suite}/IfElseUnifiesNumericBranchTails", () =>
			{
				var compiler = new ExpressionMethodCompiler();

				// Each branch tail is a non-void assignment of a different type (int vs double); the if/else is
				// built as an Expression.Condition, which previously threw because the arm types didn't match.
				var func = compiler.CompileFunc<bool, int>(
					$$"""
					  int a = 0;
					  double b = 0.0;
					  if (c) { a = 1; } else { b = 2.0; }
					  return a;
					  """,
					"c");
				Check.Equal(func(true), 1);
				Check.Equal(func(false), 0);
			});

			list.Add($"{Suite}/InstanceMemberAssignmentCoerces", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(CoercionTarget), "CoercionTarget");
				var func = compiler.CompileFunc<CoercionTarget>(
					"CoercionTarget t = new CoercionTarget(); t.Value = 3; return t;");
				Check.Equal(func().Value, 3f);
			});

			list.Add($"{Suite}/StaticFieldAssignmentCoerces", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(CoercionTarget), "CoercionTarget");
				var func = compiler.CompileFunc<float>("CoercionTarget.Shared = 7; return CoercionTarget.Shared;");
				Check.Equal(func(), 7f);
			});

			list.Add($"{Suite}/ImpossibleReturnConversionIsAPositionedCompileError", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var ex = Check.ThrowsCompile(() => compiler.CompileFunc<int>("return \"hello\";"),
					"impossible return conversion");
				Check.Contains(ex.Message, "Cannot convert", "message");
				Check.Greater(ex.Line, 0, "line");
			});

			list.Add($"{Suite}/IncompatibleTernaryBranchesIsACompileError", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var ex = Check.ThrowsCompile(
					() => compiler.CompileFunc<bool, object>("return c ? \"text\" : 1;", "c"),
					"incompatible ternary branches");
				Check.Contains(ex.Message, "incompatible", "message");
			});
		}
	}
}
