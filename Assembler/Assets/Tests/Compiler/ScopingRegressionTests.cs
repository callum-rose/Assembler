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
	/// Regression tests for issue #231: block/lambda scoping and postfix-increment
	/// codegen correctness bugs that once silently returned wrong values.
	/// </summary>
	public class ScopingRegressionTests : CompilerTestBase
	{
		// Regression tests for issue #231: scoping & codegen correctness bugs that silently
		// returned wrong values rather than erroring.

		// A variable declared inside a block must stay scoped to that block — it must not leak into the
		// surrounding method scope and read back as an unassigned default after the block closes.
		[Test]
		public void BlockScopedVariableDoesNotLeakAsDefault()
		{

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

			Assert.That(func(1), Is.EqualTo(42));
		}

		// A variable declared inside a block goes out of scope when the block closes; reading it afterwards
		// is an error (C# CS0103), not a silent default-valued leak.
		[Test]
		public void BlockScopedVariableIsOutOfScopeAfterBlock()
		{

			var ex = Assert.Throws<CompileException>(
				() => compiler.CompileFunc<int>(
					$$"""
					  if (true)
					  {
					      int inner = 5;
					  }
					  return inner;
					  """));

			Assert.That(ex.Message, Does.Contain("inner"));
		}

		// Redeclaring a name already visible in an enclosing scope is a compile error, matching C# (CS0136) —
		// rather than silently shadowing it and reading back the wrong variable.
		[Test]
		public void BlockRedeclaringEnclosingVariableIsCompileError()
		{

			var ex = Assert.Throws<CompileException>(
				() => compiler.CompileFunc<int>(
					$$"""
					  int y = 7;
					  if (true)
					  {
					      int y = 100;
					  }
					  return y;
					  """));

			Assert.That(ex.Message, Does.Contain("y").And.Contain("enclosing"));
		}

		// Two sibling blocks may each declare the same name — neither is in scope for the other, so this is
		// legal in C# and must keep compiling.
		[Test]
		public void SiblingBlocksMayReuseVariableName()
		{

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

			Assert.That(func(1), Is.EqualTo(30));
		}

		// Two sibling for-loops may each declare `i`: the first loop's variable is out of scope by the
		// second, so reusing the name is legal (matches C#).
		[Test]
		public void SiblingForLoopsMayReuseLoopVariable()
		{

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
			Assert.That(func(), Is.EqualTo(9));
		}

		// A lambda parameter shadowing a name already in scope is a compile error (C# CS0136) — not a
		// silent overwrite that deletes the outer variable.
		[Test]
		public void LambdaParameterShadowingEnclosingVariableIsCompileError()
		{
			compiler.RegisterStaticMethods(typeof(Enumerable));

			var ex = Assert.Throws<CompileException>(
				() => compiler.CompileFunc<int, int>(
					$$"""
					  var list = new List<int> { 1, 2, 3 };
					  var bigger = list.Where(x => x > 1).Count();
					  return bigger + x;
					  """,
					"x"));

			Assert.That(ex.Message, Does.Contain("x").And.Contain("enclosing"));
		}

		// Sibling lambdas in a chain may reuse a parameter name — each parameter is out of scope once its
		// lambda is parsed — so a non-colliding name keeps working end to end.
		[Test]
		public void ChainedLambdasMayReuseParameterName()
		{
			compiler.RegisterStaticMethods(typeof(Enumerable));

			var func = compiler.CompileFunc<int>(
				$$"""
				  var list = new List<int> { 1, 2, 3, 4 };
				  return list.Where(n => n > 1).Select(n => n * 2).Sum();
				  """);

			// {2,3,4} → {4,6,8} → 18.
			Assert.That(func(), Is.EqualTo(18));
		}

		// Postfix `x++` yields the value before incrementing, so `x++ + 1` is `old + 1`, not `(x+1) + 1`.
		[Test]
		public void PostfixIncrementYieldsValueBeforeIncrement()
		{

			var func = compiler.CompileFunc<int>(
				$"""
				 int x = 1;
				 return x++ + 1;
				 """);

			Assert.That(func(), Is.EqualTo(2));
		}

		// Postfix `x--` likewise yields the pre-decrement value.
		[Test]
		public void PostfixDecrementYieldsValueBeforeDecrement()
		{

			var func = compiler.CompileFunc<int>(
				$"""
				 int x = 5;
				 return x-- + 1;
				 """);

			Assert.That(func(), Is.EqualTo(6));
		}

		// Postfix increment on an indexer target also yields the pre-increment element value.
		[Test]
		public void PostfixIncrementOnIndexYieldsValueBeforeIncrement()
		{

			var func = compiler.CompileFunc<int>(
				$$"""
				  var list = new List<int> { 10, 20 };
				  int taken = list[0]++;
				  return taken * 100 + list[0];
				  """);

			// taken is the pre-increment 10; list[0] is now 11 → 10*100 + 11.
			Assert.That(func(), Is.EqualTo(1011));
		}
	}
}
