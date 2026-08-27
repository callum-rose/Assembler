using Assembler.Compiler.Compiler;

namespace Spike.CompilerHarness.Cases
{
	/// <summary>
	/// Port of <c>Assets/Tests/Compiler/ControlFlowTests.cs</c> (11 cases): if/else, while, for,
	/// continue, and the positioned non-boolean-condition compile errors. Bodies are copied verbatim
	/// from the source suite.
	/// </summary>
	public static class ControlFlowCases
	{
		private const string Suite = "ControlFlow";

		public static void Register(SpikeCaseList list)
		{
			list.Add($"{Suite}/SimpleIfElse", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<bool, int>(
					$$"""
					  if (x)
					  {
					      return 1;
					  }
					  else
					  {
					      return 0;
					  }
					  """,
					"x");

				Check.Equal(func(true), 1);
				Check.Equal(func(false), 0);
			});

			list.Add($"{Suite}/IfWithoutElse", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileAction<int>(
					$$"""
					  if (x > 5)
					  {
					      int result = 0;
					  }
					  """,
					"x");

				Check.DoesNotThrow(() => func(10), "func(10)");
				Check.DoesNotThrow(() => func(3), "func(3)");
			});

			list.Add($"{Suite}/NestedIf", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, int>(
					$$"""
					  if (x > 10)
					  {
					      if (x > 20)
					      {
					          return 2;
					      }
					      else
					      {
					          return 1;
					      }
					  }
					  else
					  {
					      return 0;
					  }
					  """,
					"x");

				Check.Equal(func(5), 0);
				Check.Equal(func(15), 1);
				Check.Equal(func(25), 2);
			});

			list.Add($"{Suite}/SimpleWhileLoop", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, int>(
					$$"""
					  int result = 0;
					  while (x < 10)
					  {
					      result += x * 3;
					      x++;
					  }
					  return result;
					  """,
					"x");

				Check.Equal(func(3), 126);
			});

			list.Add($"{Suite}/WhileLoopWithBreak", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>(
					$$"""
					  int x = 0;
					  int result = 0;
					  while (x < 10)
					  {
					      result += x;
					      x++;
					      if (x == 5)
					      {
					          break;
					      }
					  }
					  return result;
					  """);

				Check.Equal(func(), 10);
			});

			list.Add($"{Suite}/SimpleForLoop", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>(
					$$"""
					  int result = 0;
					  for (int i = 0; i < 5; i++)
					  {
					      result += i;
					  }
					  return result;
					  """);

				Check.Equal(func(), 10);
			});

			list.Add($"{Suite}/ForLoopWithMultiplication", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>(
					$$"""
					  int result = 1;
					  for (int i = 1; i <= 5; i++)
					  {
					      result *= i;
					  }
					  return result;
					  """);

				Check.Equal(func(), 120);
			});

			list.Add($"{Suite}/ContinueStatement", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int>(
					$$"""
					  int result = 0;
					  for (int i = 0; i < 6; i++)
					  {
					      if (i % 2 == 0)
					      {
					          continue;
					      }
					      result += i;
					  }
					  return result;
					  """);

				Check.Equal(func(), 9); // 1 + 3 + 5
			});

			list.Add($"{Suite}/NonBooleanWhileConditionIsAPositionedCompileError", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var ex = Check.ThrowsCompile(() => compiler.CompileAction("while (1) { break; }"),
					"non-boolean while condition");
				Check.Contains(ex.Message, "boolean", "message");
				Check.Greater(ex.Line, 0, "line");
			});

			list.Add($"{Suite}/NonBooleanIfConditionIsAPositionedCompileError", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var ex = Check.ThrowsCompile(() => compiler.CompileFunc<int>("if (5) { return 1; } return 0;"),
					"non-boolean if condition");
				Check.Contains(ex.Message, "boolean", "message");
				Check.Greater(ex.Line, 0, "line");
			});

			list.Add($"{Suite}/NonBooleanForConditionIsAPositionedCompileError", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var ex = Check.ThrowsCompile(
					() => compiler.CompileAction("for (int i = 0; i; i = i + 1) { break; }"),
					"non-boolean for condition");
				Check.Contains(ex.Message, "boolean", "message");
			});
		}
	}
}
