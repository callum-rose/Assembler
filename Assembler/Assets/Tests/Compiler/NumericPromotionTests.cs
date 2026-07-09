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
	/// Implicit numeric promotion, explicit casts, float-literal arithmetic, string-escape
	/// interpretation and numeric coercion at assignment-shaped sites.
	/// </summary>
	public class NumericPromotionTests : CompilerTestBase
	{
		// Float literal arithmetic with the 'f' suffix
		[Test]
		public void FloatLiteralArithmetic()
		{
			var func = compiler.CompileFunc<float>("return 1.5f + 2.5f;");
			Assert.That(func(), Is.EqualTo(4f).Within(0.0001f));
		}

		// Explicit numeric casts
		[Test]
		public void CastToDouble()
		{
			var func = compiler.CompileFunc<double>("return (double)7 / (double)2;");
			Assert.That(func(), Is.EqualTo(3.5));
		}

		[Test]
		public void CastToFloat()
		{
			var func = compiler.CompileFunc<float>("return (float)9 / (float)2;");
			Assert.That(func(), Is.EqualTo(4.5f).Within(0.0001f));
		}

		// String escape sequences are translated: "\n" becomes a real newline, not a literal 'n'.
		[Test]
		public void StringEscapesAreInterpreted()
		{
			var func = compiler.CompileFunc<string>("return \"a\\nb\";");
			Assert.That(func(), Is.EqualTo("a\nb"));
		}

		// Implicit numeric promotion in binary operations (issue #73)
		[Test]
		public void FloatPlusIntPromotes()
		{
			var func = compiler.CompileFunc<float, int, float>("return x + y;", "x", "y");
			Assert.That(func(1.5f, 2), Is.EqualTo(3.5f).Within(0.0001f));
		}

		[Test]
		public void IntPlusFloatPromotes()
		{
			var func = compiler.CompileFunc<int, float, float>("return x + y;", "x", "y");
			Assert.That(func(2, 1.5f), Is.EqualTo(3.5f).Within(0.0001f));
		}

		[Test]
		public void IntMinusFloatPromotes()
		{
			var func = compiler.CompileFunc<int, float, float>("return x - y;", "x", "y");
			Assert.That(func(5, 1.5f), Is.EqualTo(3.5f).Within(0.0001f));
		}

		[Test]
		public void IntTimesFloatPromotes()
		{
			var func = compiler.CompileFunc<int, float, float>("return x * y;", "x", "y");
			Assert.That(func(3, 2.5f), Is.EqualTo(7.5f).Within(0.0001f));
		}

		[Test]
		public void IntDividedByFloatPromotes()
		{
			var func = compiler.CompileFunc<int, float, float>("return x / y;", "x", "y");
			Assert.That(func(5, 2f), Is.EqualTo(2.5f).Within(0.0001f));
		}

		[Test]
		public void FloatModuloIntPromotes()
		{
			var func = compiler.CompileFunc<float, int, float>("return x % y;", "x", "y");
			Assert.That(func(5.5f, 2), Is.EqualTo(1.5f).Within(0.0001f));
		}

		[Test]
		public void IntPlusDoublePromotes()
		{
			var func = compiler.CompileFunc<int, double, double>("return x + y;", "x", "y");
			Assert.That(func(2, 1.5), Is.EqualTo(3.5).Within(0.0001));
		}

		[Test]
		public void MixedLessThanComparison()
		{
			var func = compiler.CompileFunc<float, int, bool>("return x < y;", "x", "y");
			Assert.That(func(1.5f, 2), Is.True);
			Assert.That(func(2.5f, 2), Is.False);
		}

		[Test]
		public void MixedGreaterThanOrEqualComparison()
		{
			var func = compiler.CompileFunc<int, float, bool>("return x >= y;", "x", "y");
			Assert.That(func(3, 2.5f), Is.True);
			Assert.That(func(2, 2.5f), Is.False);
		}

		[Test]
		public void MixedEqualityComparison()
		{
			var func = compiler.CompileFunc<float, int, bool>("return x == y;", "x", "y");
			Assert.That(func(2f, 2), Is.True);
			Assert.That(func(2.5f, 2), Is.False);
		}

		[Test]
		public void FloatVariablePlusEqualsInt()
		{
			var func = compiler.CompileFunc<int, float>(
				$$"""
				float total = 1.5f;
				total += x;
				return total;
				""",
				"x");
			Assert.That(func(2), Is.EqualTo(3.5f).Within(0.0001f));
		}

		[Test]
		public void FloatVariableMinusEqualsInt()
		{
			var func = compiler.CompileFunc<int, float>(
				$$"""
				float total = 5f;
				total -= x;
				return total;
				""",
				"x");
			Assert.That(func(2), Is.EqualTo(3f).Within(0.0001f));
		}

		[Test]
		public void FloatVariableTimesEqualsInt()
		{
			var func = compiler.CompileFunc<int, float>(
				$$"""
				float total = 2.5f;
				total *= x;
				return total;
				""",
				"x");
			Assert.That(func(3), Is.EqualTo(7.5f).Within(0.0001f));
		}

		[Test]
		public void IntVariablePlusEqualsFloatNarrowsBack()
		{
			var func = compiler.CompileFunc<float, int>(
				$$"""
				int total = 5;
				total += x;
				return total;
				""",
				"x");
			Assert.That(func(2.9f), Is.EqualTo(7));
		}

		// --- Numeric coercion at assignment-shaped sites (issue #230) ---

		[Test]
		public void ReturnCoercesIntLiteralToFloat()
		{
			var func = compiler.CompileFunc<float>("return 1;");
			Assert.That(func(), Is.EqualTo(1f));
		}

		[Test]
		public void ReturnCoercesDoubleLiteralToFloat()
		{
			var func = compiler.CompileFunc<float>("return 0.5;");
			Assert.That(func(), Is.EqualTo(0.5f));
		}

		[Test]
		public void ImplicitReturnCoercesToReturnType()
		{
			// No explicit `return` — the trailing expression statement is the implicit return value.
			var func = compiler.CompileFunc<float>("1;");
			Assert.That(func(), Is.EqualTo(1f));
		}

		[Test]
		public void PlainAssignCoercesToVariableType()
		{
			var func = compiler.CompileFunc<float>("float x = 0f; x = 1; return x;");
			Assert.That(func(), Is.EqualTo(1f));
		}

		[Test]
		public void DeclarationCoercesInitializer()
		{
			var func = compiler.CompileFunc<float>("float x = 1; return x;");
			Assert.That(func(), Is.EqualTo(1f));
		}

		[Test]
		public void TernaryUnifiesNumericBranches()
		{
			var func = compiler.CompileFunc<bool, double>("return c ? 1 : 2.0;", "c");
			Assert.That(func(true), Is.EqualTo(1.0));
			Assert.That(func(false), Is.EqualTo(2.0));
		}

		[Test]
		public void IfElseUnifiesNumericBranchTails()
		{
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
			Assert.That(func(true), Is.EqualTo(1));
			Assert.That(func(false), Is.EqualTo(0));
		}

		[Test]
		public void InstanceMemberAssignmentCoerces()
		{
			compiler.RegisterType(typeof(CoercionTarget), "CoercionTarget");
			var func = compiler.CompileFunc<CoercionTarget>(
				"CoercionTarget t = new CoercionTarget(); t.Value = 3; return t;");
			Assert.That(func().Value, Is.EqualTo(3f));
		}

		[Test]
		public void StaticFieldAssignmentCoerces()
		{
			compiler.RegisterType(typeof(CoercionTarget), "CoercionTarget");
			var func = compiler.CompileFunc<float>("CoercionTarget.Shared = 7; return CoercionTarget.Shared;");
			Assert.That(func(), Is.EqualTo(7f));
		}

		[Test]
		public void ImpossibleReturnConversionIsAPositionedCompileError()
		{
			var ex = Assert.Throws<CompileException>(() => compiler.CompileFunc<int>("return \"hello\";"));
			Assert.That(ex.Message, Does.Contain("Cannot convert"));
			Assert.That(ex.Line, Is.GreaterThan(0));
		}

		[Test]
		public void IncompatibleTernaryBranchesIsACompileError()
		{
			var ex = Assert.Throws<CompileException>(
				() => compiler.CompileFunc<bool, object>("return c ? \"text\" : 1;", "c"));
			Assert.That(ex.Message, Does.Contain("incompatible"));
		}
	}
}
