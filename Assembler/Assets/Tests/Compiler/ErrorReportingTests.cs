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
	/// Compiler error-reporting (positioned messages) and cross-expression call tests.
	/// </summary>
	public class ErrorReportingTests : CompilerTestBase
	{
		// --- Cross-expression calls (issue #72) ---

		[Test]
		public void RegisteredExpressionCanBeCalledByAnother()
		{

			Func<int, int, int> add = (a, b) => a + b;
			compiler.RegisterExpression("add", add, new[] { typeof(int), typeof(int) }, typeof(int));

			var func = compiler.CompileFunc<int, int>("return add(x, 10);", "x");

			Assert.That(func(5), Is.EqualTo(15));
		}

		[Test]
		public void RegisteredExpressionCallsAreNested()
		{

			Func<int, int, int> add = (a, b) => a + b;
			compiler.RegisterExpression("add", add, new[] { typeof(int), typeof(int) }, typeof(int));

			// "doubleAdd" is itself a compiled expression that calls "add", then is
			// registered and called by a third expression -> nested call chain.
			var doubleAdd = compiler.CompileFunc<int, int, int>("return add(add(a, b), b);", "a", "b");
			compiler.RegisterExpression("doubleAdd", doubleAdd, new[] { typeof(int), typeof(int) }, typeof(int));

			var func = compiler.CompileFunc<int, int>("return doubleAdd(x, 1);", "x");

			Assert.That(func(5), Is.EqualTo(7));
		}

		[Test]
		public void RegisteredExpressionConvertsArgumentTypes()
		{

			Func<float, float> half = v => v * 0.5f;
			compiler.RegisterExpression("half", half, new[] { typeof(float) }, typeof(float));

			// Passes an int literal where the callee expects a float.
			var func = compiler.CompileFunc<float>("return half(10);");

			Assert.That(func(), Is.EqualTo(5f).Within(0.001f));
		}

		// --- Error reporting ---

		[Test]
		public void StringEscapeSequencesAreTranslated()
		{
			var func = compiler.CompileFunc<string>("return \"a\\nb\\tc\";");

			Assert.That(func(), Is.EqualTo("a\nb\tc"));
		}

		[Test]
		public void UnterminatedStringIsACompileError()
		{

			var ex = Assert.Throws<CompileException>(() => compiler.CompileFunc<string>("return \"oops;"));
			Assert.That(ex.Message, Does.Contain("Unterminated string"));
		}

		[Test]
		public void MalformedNumberIsACompileError()
		{

			var ex = Assert.Throws<CompileException>(() => compiler.CompileFunc<double>("return 1.2.3;"));
			Assert.That(ex.Message, Does.Contain("Malformed number"));
		}

		[Test]
		public void UnrecognisedEscapeIsACompileError()
		{

			Assert.Throws<CompileException>(() => compiler.CompileFunc<string>("return \"a\\qb\";"));
		}

		[Test]
		public void AssigningUndeclaredVariableReportsUnknownIdentifier()
		{

			var ex = Assert.Throws<CompileException>(() => compiler.CompileAction("missing = 5;"));
			Assert.That(ex.Message, Does.Contain("Unknown identifier"));
			Assert.That(ex.Message, Does.Contain("missing"));
		}

		[Test]
		public void CompoundAssignUndeclaredVariableReportsUnknownIdentifier()
		{

			var ex = Assert.Throws<CompileException>(() => compiler.CompileAction("missing += 5;"));
			Assert.That(ex.Message, Does.Contain("Unknown identifier"));
		}

		[Test]
		public void CompileExceptionCarriesLineAndColumn()
		{

			// Unexpected character '@' on the second line.
			var ex = Assert.Throws<CompileException>(() => compiler.CompileFunc<int>("int x = 1;\nreturn @;"));
			Assert.That(ex.Line, Is.EqualTo(2));
			Assert.That(ex.Column, Is.GreaterThan(0));
		}

		[Test]
		public void UnexpectedCharacterIsACompileError()
		{

			Assert.Throws<CompileException>(() => compiler.CompileFunc<int>("return 1 @ 2;"));
		}
	}
}
