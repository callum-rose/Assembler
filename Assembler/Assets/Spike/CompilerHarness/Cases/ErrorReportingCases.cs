using System;
using Assembler.Compiler.Compiler;

namespace Spike.CompilerHarness.Cases
{
	/// <summary>
	/// Port of <c>Assets/Tests/Compiler/ErrorReportingTests.cs</c> (11 cases): compiler error reporting
	/// (positioned messages) and cross-expression calls. Bodies are copied verbatim from the source suite.
	/// The cross-expression cases matter for AOT because <c>RegisterExpression</c> stores a
	/// <see cref="Delegate"/> that a later compile invokes through a constructed call site.
	/// </summary>
	public static class ErrorReportingCases
	{
		private const string Suite = "ErrorReporting";

		public static void Register(SpikeCaseList list)
		{
			list.Add($"{Suite}/RegisteredExpressionCanBeCalledByAnother", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				Func<int, int, int> add = (a, b) => a + b;
				compiler.RegisterExpression("add", add, new[] { typeof(int), typeof(int) }, typeof(int));

				var func = compiler.CompileFunc<int, int>("return add(x, 10);", "x");

				Check.Equal(func(5), 15);
			});

			list.Add($"{Suite}/RegisteredExpressionCallsAreNested", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				Func<int, int, int> add = (a, b) => a + b;
				compiler.RegisterExpression("add", add, new[] { typeof(int), typeof(int) }, typeof(int));

				// "doubleAdd" is itself a compiled expression that calls "add", then is
				// registered and called by a third expression -> nested call chain.
				var doubleAdd = compiler.CompileFunc<int, int, int>("return add(add(a, b), b);", "a", "b");
				compiler.RegisterExpression("doubleAdd", doubleAdd, new[] { typeof(int), typeof(int) }, typeof(int));

				var func = compiler.CompileFunc<int, int>("return doubleAdd(x, 1);", "x");

				Check.Equal(func(5), 7);
			});

			list.Add($"{Suite}/RegisteredExpressionConvertsArgumentTypes", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				Func<float, float> half = v => v * 0.5f;
				compiler.RegisterExpression("half", half, new[] { typeof(float) }, typeof(float));

				// Passes an int literal where the callee expects a float.
				var func = compiler.CompileFunc<float>("return half(10);");

				Check.Approx(func(), 5f, 0.001f);
			});

			list.Add($"{Suite}/StringEscapeSequencesAreTranslated", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<string>("return \"a\\nb\\tc\";");

				Check.Equal(func(), "a\nb\tc");
			});

			list.Add($"{Suite}/UnterminatedStringIsACompileError", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var ex = Check.ThrowsCompile(() => compiler.CompileFunc<string>("return \"oops;"),
					"unterminated string");
				Check.Contains(ex.Message, "Unterminated string", "message");
			});

			list.Add($"{Suite}/MalformedNumberIsACompileError", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var ex = Check.ThrowsCompile(() => compiler.CompileFunc<double>("return 1.2.3;"),
					"malformed number");
				Check.Contains(ex.Message, "Malformed number", "message");
			});

			list.Add($"{Suite}/UnrecognisedEscapeIsACompileError", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				Check.ThrowsCompile(() => compiler.CompileFunc<string>("return \"a\\qb\";"),
					"unrecognised escape");
			});

			list.Add($"{Suite}/AssigningUndeclaredVariableReportsUnknownIdentifier", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var ex = Check.ThrowsCompile(() => compiler.CompileAction("missing = 5;"),
					"assigning an undeclared variable");
				Check.Contains(ex.Message, "Unknown identifier", "message");
				Check.Contains(ex.Message, "missing", "message");
			});

			list.Add($"{Suite}/CompoundAssignUndeclaredVariableReportsUnknownIdentifier", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var ex = Check.ThrowsCompile(() => compiler.CompileAction("missing += 5;"),
					"compound-assigning an undeclared variable");
				Check.Contains(ex.Message, "Unknown identifier", "message");
			});

			list.Add($"{Suite}/CompileExceptionCarriesLineAndColumn", () =>
			{
				var compiler = new ExpressionMethodCompiler();

				// Unexpected character '@' on the second line.
				var ex = Check.ThrowsCompile(() => compiler.CompileFunc<int>("int x = 1;\nreturn @;"),
					"unexpected character on line 2");
				Check.Equal(ex.Line, 2, "line");
				Check.Greater(ex.Column, 0, "column");
			});

			list.Add($"{Suite}/UnexpectedCharacterIsACompileError", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				Check.ThrowsCompile(() => compiler.CompileFunc<int>("return 1 @ 2;"), "unexpected character");
			});
		}
	}
}
