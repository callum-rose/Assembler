using Assembler.Compiler.Compiler;
using UnityEngine;

namespace Spike.CompilerHarness.Cases
{
	/// <summary>
	/// Port of <c>Assets/Tests/Compiler/ArithmeticAndOperatorTests.cs</c> (38 cases): arithmetic,
	/// comparison/logical/boolean, variable assignment, compound-assign, increment/decrement, ternary,
	/// XOR and Vector3 operators. Bodies are copied verbatim from the source suite.
	/// </summary>
	public static class ArithmeticAndOperatorCases
	{
		private const string Suite = "ArithmeticAndOperator";

		public static void Register(SpikeCaseList list)
		{
			list.Add($"{Suite}/CompilerTestsSimplePasses", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var expression = "new UnityEngine.Vector3(0, UnityEngine.Random.Range(-2f, 2f), 0);";

				var compiled = compiler.Compile(expression, typeof(Vector3), out _);

				var result = compiled.DynamicInvoke();

				Check.NotNull(compiled, "compiled delegate");
				Check.IsInstanceOf(typeof(Vector3), result, "result");
			});

			list.Add($"{Suite}/SimpleAddition", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>("return 1 + 4;");
				Check.Equal(func(), 5);
			});

			list.Add($"{Suite}/SimpleSubtraction", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>("return 10 - 3;");
				Check.Equal(func(), 7);
			});

			list.Add($"{Suite}/SimpleMultiplication", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>("return 5 * 6;");
				Check.Equal(func(), 30);
			});

			list.Add($"{Suite}/SimpleDivision", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>("return 20 / 4;");
				Check.Equal(func(), 5);
			});

			list.Add($"{Suite}/SimpleModulo", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>("return 17 % 5;");
				Check.Equal(func(), 2);
			});

			list.Add($"{Suite}/ComplexArithmetic", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>("return 2 + 3 * 4 - 10 / 2;");
				Check.Equal(func(), 9);
			});

			list.Add($"{Suite}/LessThanComparison", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, bool>("return x < 10;", "x");
				Check.True(func(5), "func(5)");
				Check.False(func(15), "func(15)");
			});

			list.Add($"{Suite}/GreaterThanComparison", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, bool>("return x > 10;", "x");
				Check.True(func(15), "func(15)");
				Check.False(func(5), "func(5)");
			});

			list.Add($"{Suite}/EqualityComparison", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, bool>("return x == 10;", "x");
				Check.True(func(10), "func(10)");
				Check.False(func(5), "func(5)");
			});

			list.Add($"{Suite}/NotEqualComparison", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, bool>("return x != 10;", "x");
				Check.True(func(5), "func(5)");
				Check.False(func(10), "func(10)");
			});

			list.Add($"{Suite}/LessThanOrEqualComparison", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, bool>("return x <= 10;", "x");
				Check.True(func(10), "func(10)");
				Check.True(func(5), "func(5)");
				Check.False(func(15), "func(15)");
			});

			list.Add($"{Suite}/GreaterThanOrEqualComparison", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, bool>("return x >= 10;", "x");
				Check.True(func(10), "func(10)");
				Check.True(func(15), "func(15)");
				Check.False(func(5), "func(5)");
			});

			list.Add($"{Suite}/LogicalAnd", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, bool>("return x > 5 && x < 15;", "x");
				Check.True(func(10), "func(10)");
				Check.False(func(3), "func(3)");
				Check.False(func(20), "func(20)");
			});

			list.Add($"{Suite}/LogicalOr", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, bool>("return x < 5 || x > 15;", "x");
				Check.True(func(3), "func(3)");
				Check.True(func(20), "func(20)");
				Check.False(func(10), "func(10)");
			});

			list.Add($"{Suite}/LogicalNot", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<bool, bool>("return !x;", "x");
				Check.False(func(true), "func(true)");
				Check.True(func(false), "func(false)");
			});

			list.Add($"{Suite}/BooleanLiteralTrue", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<bool>("return true;");
				Check.True(func(), "func()");
			});

			list.Add($"{Suite}/BooleanLiteralFalseVariable", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<bool>("bool b = false; return b;");
				Check.False(func(), "func()");
			});

			list.Add($"{Suite}/BooleanLiteralFlagPattern", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<bool, bool>(
					$$"""
					  bool ok = true;
					  if (x) { ok = false; }
					  return ok;
					  """,
					"x");
				Check.True(func(false), "func(false)");
				Check.False(func(true), "func(true)");
			});

			list.Add($"{Suite}/VariableDeclaration", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>(
					$$"""
					  int x = 10;
					  return x;
					  """);

				Check.Equal(func(), 10);
			});

			list.Add($"{Suite}/VariableAssignment", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>(
					$$"""
					  int x = 10;
					  x = 20;
					  return x;
					  """);

				Check.Equal(func(), 20);
			});

			list.Add($"{Suite}/MultipleVariables", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>(
					$"""
					 int x = 10;
					 int y = 20;
					 return x + y;
					 """);

				Check.Equal(func(), 30);
			});

			list.Add($"{Suite}/PlusAssignment", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, int>(
					$"""
					 int result = 10;
					 result += x;
					 return result;
					 """,
					"x");

				Check.Equal(func(5), 15);
			});

			list.Add($"{Suite}/MinusAssignment", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, int>(
					$"""
					 int result = 10;
					 result -= x;
					 return result;
					 """,
					"x");

				Check.Equal(func(3), 7);
			});

			list.Add($"{Suite}/IncrementOperator", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>(
					$"""
					 int x = 5;
					 x++;
					 return x;
					 """);

				Check.Equal(func(), 6);
			});

			list.Add($"{Suite}/DecrementOperator", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>(
					$"""
					 int x = 5;
					 x--;
					 return x;
					 """);

				Check.Equal(func(), 4);
			});

			list.Add($"{Suite}/SimpleTernary", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, int>("return x > 10 ? 1 : 0;", "x");
				Check.Equal(func(15), 1);
				Check.Equal(func(5), 0);
			});

			list.Add($"{Suite}/NestedTernary", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, int>(
					"return x > 10 ? (x > 20 ? 2 : 1) : 0;",
					"x");

				Check.Equal(func(5), 0);
				Check.Equal(func(15), 1);
				Check.Equal(func(25), 2);
			});

			list.Add($"{Suite}/MultiplyAssignment", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, int>(
					$"""
					 int result = 10;
					 result *= x;
					 return result;
					 """,
					"x");

				Check.Equal(func(3), 30);
			});

			list.Add($"{Suite}/DivideAssignment", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, int>(
					$"""
					 int result = 20;
					 result /= x;
					 return result;
					 """,
					"x");

				Check.Equal(func(4), 5);
			});

			list.Add($"{Suite}/BooleanXor", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<bool, bool, bool>("return a ^ b;", "a", "b");

				Check.True(func(true, false), "func(true, false)");
				Check.False(func(true, true), "func(true, true)");
				Check.False(func(false, false), "func(false, false)");
			});

			list.Add($"{Suite}/IntegerXor", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>("return 6 ^ 3;");

				Check.Equal(func(), 5);
			});

			list.Add($"{Suite}/XorBindsLooserThanEqualityAndTighterThanLogicalAnd", () =>
			{
				var compiler = new ExpressionMethodCompiler();

				// Parsed as (1 == 1) ^ (2 == 3) => true ^ false => true.
				var xorOverEquality = compiler.CompileFunc<bool>("return 1 == 1 ^ 2 == 3;");
				Check.True(xorOverEquality(), "xorOverEquality()");

				// Parsed as true && (true ^ true) => true && false => false.
				var andOverXor = compiler.CompileFunc<bool>("return true && true ^ true;");
				Check.False(andOverXor(), "andOverXor()");
			});

			list.Add($"{Suite}/Vector3UnaryNegation", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<Vector3, Vector3>("return -v;", "v");

				Check.Equal(func(new Vector3(1, -2, 3)), new Vector3(-1, 2, -3));
			});

			list.Add($"{Suite}/Vector3Addition", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<Vector3, Vector3, Vector3>("return a + b;", "a", "b");

				Check.Equal(func(new Vector3(1, 2, 3), new Vector3(4, 5, 6)), new Vector3(5, 7, 9));
			});

			list.Add($"{Suite}/Vector3MultiplyByFloatScalar", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<Vector3, Vector3>("return v * 2f;", "v");

				Check.Equal(func(new Vector3(1, 2, 3)), new Vector3(2, 4, 6));
			});

			list.Add($"{Suite}/Vector3MultiplyByIntScalarPromotesToFloat", () =>
			{
				var compiler = new ExpressionMethodCompiler();

				// `2` lexes to int; the vector operator takes a float, so the scalar must widen.
				var func = compiler.CompileFunc<Vector3, Vector3>("return v * 2;", "v");

				Check.Equal(func(new Vector3(1, 2, 3)), new Vector3(2, 4, 6));
			});

			list.Add($"{Suite}/Vector3DivideByScalar", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<Vector3, Vector3>("return v / 2;", "v");

				Check.Equal(func(new Vector3(2, 4, 6)), new Vector3(1, 2, 3));
			});
		}
	}
}
