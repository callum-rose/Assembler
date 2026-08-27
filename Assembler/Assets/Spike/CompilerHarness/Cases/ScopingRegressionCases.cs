using System.Linq;
using Assembler.Compiler.Compiler;

namespace Spike.CompilerHarness.Cases
{
	/// <summary>
	/// Port of <c>Assets/Tests/Compiler/ScopingRegressionTests.cs</c> (10 cases): the issue #231
	/// block/lambda scoping and postfix-increment codegen bugs that once silently returned wrong values.
	/// Bodies are copied verbatim from the source suite. These are the cases that catch a wrong answer
	/// rather than a crash — the failure mode a compile-only AOT check would sail straight past.
	/// </summary>
	public static class ScopingRegressionCases
	{
		private const string Suite = "ScopingRegression";

		public static void Register(SpikeCaseList list)
		{
			list.Add($"{Suite}/BlockScopedVariableDoesNotLeakAsDefault", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, int>(
					$$"""
					  int total = 0;
					  if (x > 0)
					  {
					      int local = 41;
					      local = local + 1;
					      total = local;
					  }
					  return total;
					  """,
					"x");

				Check.Equal(func(1), 42);
			});

			list.Add($"{Suite}/BlockScopedVariableIsOutOfScopeAfterBlock", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var ex = Check.ThrowsCompile(
					() => compiler.CompileFunc<int>(
						$$"""
						  if (true)
						  {
						      int inner = 5;
						  }
						  return inner;
						  """),
					"block-scoped variable read after the block");

				Check.Contains(ex.Message, "inner", "message");
			});

			list.Add($"{Suite}/BlockRedeclaringEnclosingVariableIsCompileError", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var ex = Check.ThrowsCompile(
					() => compiler.CompileFunc<int>(
						$$"""
						  int y = 7;
						  if (true)
						  {
						      int y = 100;
						  }
						  return y;
						  """),
					"block redeclaring an enclosing variable");

				Check.Contains(ex.Message, "y", "message");
				Check.Contains(ex.Message, "enclosing", "message");
			});

			list.Add($"{Suite}/SiblingBlocksMayReuseVariableName", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, int>(
					$$"""
					  int total = 0;
					  if (x > 0)
					  {
					      int y = 10;
					      total = total + y;
					  }
					  if (x > 0)
					  {
					      int y = 20;
					      total = total + y;
					  }
					  return total;
					  """,
					"x");

				Check.Equal(func(1), 30);
			});

			list.Add($"{Suite}/SiblingForLoopsMayReuseLoopVariable", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>(
					$$"""
					  int total = 0;
					  for (int i = 0; i < 3; i++)
					  {
					      total = total + i;
					  }
					  for (int i = 0; i < 4; i++)
					  {
					      total = total + i;
					  }
					  return total;
					  """);

				// (0+1+2) + (0+1+2+3) = 3 + 6 = 9.
				Check.Equal(func(), 9);
			});

			list.Add($"{Suite}/LambdaParameterShadowingEnclosingVariableIsCompileError", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterStaticMethods(typeof(Enumerable));

				var ex = Check.ThrowsCompile(
					() => compiler.CompileFunc<int, int>(
						$$"""
						  var list = new List<int> { 1, 2, 3 };
						  var bigger = list.Where(x => x > 1).Count();
						  return bigger + x;
						  """,
						"x"),
					"lambda parameter shadowing an enclosing variable");

				Check.Contains(ex.Message, "x", "message");
				Check.Contains(ex.Message, "enclosing", "message");
			});

			list.Add($"{Suite}/ChainedLambdasMayReuseParameterName", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterStaticMethods(typeof(Enumerable));

				var func = compiler.CompileFunc<int>(
					$$"""
					  var list = new List<int> { 1, 2, 3, 4 };
					  return list.Where(n => n > 1).Select(n => n * 2).Sum();
					  """);

				// {2,3,4} -> {4,6,8} -> 18.
				Check.Equal(func(), 18);
			});

			list.Add($"{Suite}/PostfixIncrementYieldsValueBeforeIncrement", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>(
					$"""
					 int x = 1;
					 return x++ + 1;
					 """);

				Check.Equal(func(), 2);
			});

			list.Add($"{Suite}/PostfixDecrementYieldsValueBeforeDecrement", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>(
					$"""
					 int x = 5;
					 return x-- + 1;
					 """);

				Check.Equal(func(), 6);
			});

			list.Add($"{Suite}/PostfixIncrementOnIndexYieldsValueBeforeIncrement", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>(
					$$"""
					  var list = new List<int> { 10, 20 };
					  int taken = list[0]++;
					  return taken * 100 + list[0];
					  """);

				// taken is the pre-increment 10; list[0] is now 11 -> 10*100 + 11.
				Check.Equal(func(), 1011);
			});
		}
	}
}
