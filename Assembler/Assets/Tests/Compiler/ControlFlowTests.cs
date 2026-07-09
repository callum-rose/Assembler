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
	/// Control-flow tests: if/else, while, for, continue, and the positioned
	/// non-boolean-condition compile errors.
	/// </summary>
	public class ControlFlowTests : CompilerTestBase
	{
		// If/else tests
		[Test]
		public void SimpleIfElse()
		{

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

			Assert.That(func(true), Is.EqualTo(1));
			Assert.That(func(false), Is.EqualTo(0));
		}

		[Test]
		public void IfWithoutElse()
		{

			var func = compiler.CompileAction<int>(
				$$"""
				  if (x > 5)
				  {
				      int result = 0;
				  }
				  """,
				"x");

			Assert.DoesNotThrow(() => func(10));
			Assert.DoesNotThrow(() => func(3));
		}

		[Test]
		public void NestedIf()
		{

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

			Assert.That(func(5), Is.EqualTo(0));
			Assert.That(func(15), Is.EqualTo(1));
			Assert.That(func(25), Is.EqualTo(2));
		}

		// While loop tests
		[Test]
		public void SimpleWhileLoop()
		{

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

			Assert.That(func(3), Is.EqualTo(126));
		}

		[Test]
		public void WhileLoopWithBreak()
		{

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

			Assert.That(func(), Is.EqualTo(10));
		}

		// For loop tests
		[Test]
		public void SimpleForLoop()
		{

			var func = compiler.CompileFunc<int>(
				$$"""
				  int result = 0;
				  for (int i = 0; i < 5; i++)
				  {
				      result += i;
				  }
				  return result;
				  """);

			Assert.That(func(), Is.EqualTo(10));
		}

		[Test]
		public void ForLoopWithMultiplication()
		{

			var func = compiler.CompileFunc<int>(
				$$"""
				  int result = 1;
				  for (int i = 1; i <= 5; i++)
				  {
				      result *= i;
				  }
				  return result;
				  """);

			Assert.That(func(), Is.EqualTo(120));
		}

		// continue
		[Test]
		public void ContinueStatement()
		{

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

			Assert.That(func(), Is.EqualTo(9)); // 1 + 3 + 5
		}

		// --- Control-flow conditions must be boolean (positioned, not a raw ArgumentException) ---

		[Test]
		public void NonBooleanWhileConditionIsAPositionedCompileError()
		{
			var ex = Assert.Throws<CompileException>(() => compiler.CompileAction("while (1) { break; }"));
			Assert.That(ex.Message, Does.Contain("boolean"));
			Assert.That(ex.Line, Is.GreaterThan(0));
		}

		[Test]
		public void NonBooleanIfConditionIsAPositionedCompileError()
		{
			var ex = Assert.Throws<CompileException>(() => compiler.CompileFunc<int>("if (5) { return 1; } return 0;"));
			Assert.That(ex.Message, Does.Contain("boolean"));
			Assert.That(ex.Line, Is.GreaterThan(0));
		}

		[Test]
		public void NonBooleanForConditionIsAPositionedCompileError()
		{
			var ex = Assert.Throws<CompileException>(
				() => compiler.CompileAction("for (int i = 0; i; i = i + 1) { break; }"));
			Assert.That(ex.Message, Does.Contain("boolean"));
		}
	}
}
