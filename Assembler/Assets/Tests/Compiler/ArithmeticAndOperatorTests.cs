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
	/// Arithmetic, comparison/logical/boolean, variable assignment, compound-assign,
	/// increment/decrement, ternary, XOR and Vector3 operator tests.
	/// </summary>
	public class ArithmeticAndOperatorTests : CompilerTestBase
	{
		[Test]
		public void CompilerTestsSimplePasses()
		{
			var expression = "new UnityEngine.Vector3(0, UnityEngine.Random.Range(-2f, 2f), 0);";

			var compiled = compiler.Compile(expression, typeof(Vector3), out _);

			var result = compiled.DynamicInvoke();

			Assert.IsNotNull(compiled);
			Assert.IsInstanceOf<Vector3>(result);
		}

		// Basic arithmetic tests
		[Test]
		public void SimpleAddition()
		{
			var func = compiler.CompileFunc<int>("return 1 + 4;");
			Assert.That(func(), Is.EqualTo(5));
		}

		[Test]
		public void SimpleSubtraction()
		{
			var func = compiler.CompileFunc<int>("return 10 - 3;");
			Assert.That(func(), Is.EqualTo(7));
		}

		[Test]
		public void SimpleMultiplication()
		{
			var func = compiler.CompileFunc<int>("return 5 * 6;");
			Assert.That(func(), Is.EqualTo(30));
		}

		[Test]
		public void SimpleDivision()
		{
			var func = compiler.CompileFunc<int>("return 20 / 4;");
			Assert.That(func(), Is.EqualTo(5));
		}

		[Test]
		public void SimpleModulo()
		{
			var func = compiler.CompileFunc<int>("return 17 % 5;");
			Assert.That(func(), Is.EqualTo(2));
		}

		[Test]
		public void ComplexArithmetic()
		{
			var func = compiler.CompileFunc<int>("return 2 + 3 * 4 - 10 / 2;");
			Assert.That(func(), Is.EqualTo(9));
		}

		// Comparison operators
		[Test]
		public void LessThanComparison()
		{
			var func = compiler.CompileFunc<int, bool>("return x < 10;", "x");
			Assert.That(func(5), Is.True);
			Assert.That(func(15), Is.False);
		}

		[Test]
		public void GreaterThanComparison()
		{
			var func = compiler.CompileFunc<int, bool>("return x > 10;", "x");
			Assert.That(func(15), Is.True);
			Assert.That(func(5), Is.False);
		}

		[Test]
		public void EqualityComparison()
		{
			var func = compiler.CompileFunc<int, bool>("return x == 10;", "x");
			Assert.That(func(10), Is.True);
			Assert.That(func(5), Is.False);
		}

		[Test]
		public void NotEqualComparison()
		{
			var func = compiler.CompileFunc<int, bool>("return x != 10;", "x");
			Assert.That(func(5), Is.True);
			Assert.That(func(10), Is.False);
		}

		[Test]
		public void LessThanOrEqualComparison()
		{
			var func = compiler.CompileFunc<int, bool>("return x <= 10;", "x");
			Assert.That(func(10), Is.True);
			Assert.That(func(5), Is.True);
			Assert.That(func(15), Is.False);
		}

		[Test]
		public void GreaterThanOrEqualComparison()
		{
			var func = compiler.CompileFunc<int, bool>("return x >= 10;", "x");
			Assert.That(func(10), Is.True);
			Assert.That(func(15), Is.True);
			Assert.That(func(5), Is.False);
		}

		// Logical operators
		[Test]
		public void LogicalAnd()
		{
			var func = compiler.CompileFunc<int, bool>("return x > 5 && x < 15;", "x");
			Assert.That(func(10), Is.True);
			Assert.That(func(3), Is.False);
			Assert.That(func(20), Is.False);
		}

		[Test]
		public void LogicalOr()
		{
			var func = compiler.CompileFunc<int, bool>("return x < 5 || x > 15;", "x");
			Assert.That(func(3), Is.True);
			Assert.That(func(20), Is.True);
			Assert.That(func(10), Is.False);
		}

		[Test]
		public void LogicalNot()
		{
			var func = compiler.CompileFunc<bool, bool>("return !x;", "x");
			Assert.That(func(true), Is.False);
			Assert.That(func(false), Is.True);
		}

		// Boolean literals
		[Test]
		public void BooleanLiteralTrue()
		{
			var func = compiler.CompileFunc<bool>("return true;");
			Assert.That(func(), Is.True);
		}

		[Test]
		public void BooleanLiteralFalseVariable()
		{
			var func = compiler.CompileFunc<bool>("bool b = false; return b;");
			Assert.That(func(), Is.False);
		}

		[Test]
		public void BooleanLiteralFlagPattern()
		{
			var func = compiler.CompileFunc<bool, bool>(
				$$"""
				  bool ok = true;
				  if (x) { ok = false; }
				  return ok;
				  """,
				"x");
			Assert.That(func(false), Is.True);
			Assert.That(func(true), Is.False);
		}

		// Variable declaration and assignment
		[Test]
		public void VariableDeclaration()
		{

			var func = compiler.CompileFunc<int>(
				$$"""
				  int x = 10;
				  return x;
				  """);

			Assert.That(func(), Is.EqualTo(10));
		}

		[Test]
		public void VariableAssignment()
		{

			var func = compiler.CompileFunc<int>(
				$$"""
				  int x = 10;
				  x = 20;
				  return x;
				  """);

			Assert.That(func(), Is.EqualTo(20));
		}

		[Test]
		public void MultipleVariables()
		{

			var func = compiler.CompileFunc<int>(
				$"""
				 int x = 10;
				 int y = 20;
				 return x + y;
				 """);

			Assert.That(func(), Is.EqualTo(30));
		}

		// Compound assignment operators
		[Test]
		public void PlusAssignment()
		{

			var func = compiler.CompileFunc<int, int>(
				$"""
				 int result = 10;
				 result += x;
				 return result;
				 """,
				"x");

			Assert.That(func(5), Is.EqualTo(15));
		}

		[Test]
		public void MinusAssignment()
		{

			var func = compiler.CompileFunc<int, int>(
				$"""
				 int result = 10;
				 result -= x;
				 return result;
				 """,
				"x");

			Assert.That(func(3), Is.EqualTo(7));
		}

		// Increment and decrement operators
		[Test]
		public void IncrementOperator()
		{

			var func = compiler.CompileFunc<int>(
				$"""
				 int x = 5;
				 x++;
				 return x;
				 """);

			Assert.That(func(), Is.EqualTo(6));
		}

		[Test]
		public void DecrementOperator()
		{

			var func = compiler.CompileFunc<int>(
				$"""
				 int x = 5;
				 x--;
				 return x;
				 """);

			Assert.That(func(), Is.EqualTo(4));
		}

		// Ternary operator tests
		[Test]
		public void SimpleTernary()
		{
			var func = compiler.CompileFunc<int, int>("return x > 10 ? 1 : 0;", "x");
			Assert.That(func(15), Is.EqualTo(1));
			Assert.That(func(5), Is.EqualTo(0));
		}

		[Test]
		public void NestedTernary()
		{

			var func = compiler.CompileFunc<int, int>(
				"return x > 10 ? (x > 20 ? 2 : 1) : 0;",
				"x");

			Assert.That(func(5), Is.EqualTo(0));
			Assert.That(func(15), Is.EqualTo(1));
			Assert.That(func(25), Is.EqualTo(2));
		}

		// Compound assignment operators (multiply / divide)
		[Test]
		public void MultiplyAssignment()
		{

			var func = compiler.CompileFunc<int, int>(
				$"""
				 int result = 10;
				 result *= x;
				 return result;
				 """,
				"x");

			Assert.That(func(3), Is.EqualTo(30));
		}

		[Test]
		public void DivideAssignment()
		{

			var func = compiler.CompileFunc<int, int>(
				$"""
				 int result = 20;
				 result /= x;
				 return result;
				 """,
				"x");

			Assert.That(func(4), Is.EqualTo(5));
		}

		// --- ^ (XOR) operator ---

		[Test]
		public void BooleanXor()
		{
			var func = compiler.CompileFunc<bool, bool, bool>("return a ^ b;", "a", "b");

			Assert.That(func(true, false), Is.True);
			Assert.That(func(true, true), Is.False);
			Assert.That(func(false, false), Is.False);
		}

		[Test]
		public void IntegerXor()
		{
			var func = compiler.CompileFunc<int>("return 6 ^ 3;");

			Assert.That(func(), Is.EqualTo(5));
		}

		[Test]
		public void XorBindsLooserThanEqualityAndTighterThanLogicalAnd()
		{

			// Parsed as (1 == 1) ^ (2 == 3) => true ^ false => true.
			var xorOverEquality = compiler.CompileFunc<bool>("return 1 == 1 ^ 2 == 3;");
			Assert.That(xorOverEquality(), Is.True);

			// Parsed as true && (true ^ true) => true && false => false.
			var andOverXor = compiler.CompileFunc<bool>("return true && true ^ true;");
			Assert.That(andOverXor(), Is.False);
		}

		// --- Vector3 operators ---

		[Test]
		public void Vector3UnaryNegation()
		{
			var func = compiler.CompileFunc<Vector3, Vector3>("return -v;", "v");

			Assert.That(func(new Vector3(1, -2, 3)), Is.EqualTo(new Vector3(-1, 2, -3)));
		}

		[Test]
		public void Vector3Addition()
		{
			var func = compiler.CompileFunc<Vector3, Vector3, Vector3>("return a + b;", "a", "b");

			Assert.That(func(new Vector3(1, 2, 3), new Vector3(4, 5, 6)), Is.EqualTo(new Vector3(5, 7, 9)));
		}

		[Test]
		public void Vector3MultiplyByFloatScalar()
		{
			var func = compiler.CompileFunc<Vector3, Vector3>("return v * 2f;", "v");

			Assert.That(func(new Vector3(1, 2, 3)), Is.EqualTo(new Vector3(2, 4, 6)));
		}

		[Test]
		public void Vector3MultiplyByIntScalarPromotesToFloat()
		{
			// `2` lexes to int; the vector operator takes a float, so the scalar must widen.
			var func = compiler.CompileFunc<Vector3, Vector3>("return v * 2;", "v");

			Assert.That(func(new Vector3(1, 2, 3)), Is.EqualTo(new Vector3(2, 4, 6)));
		}

		[Test]
		public void Vector3DivideByScalar()
		{
			var func = compiler.CompileFunc<Vector3, Vector3>("return v / 2;", "v");

			Assert.That(func(new Vector3(2, 4, 6)), Is.EqualTo(new Vector3(1, 2, 3)));
		}
	}
}
