using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Compiler.Compiler;
using NUnit.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tests.Compiler
{
	public class CompilerTests
	{
		[Test]
		public void CompilerTestsSimplePasses()
		{
			var compiler = new ExpressionMethodCompiler();
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
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int>("return 1 + 4;");
			Assert.That(func(), Is.EqualTo(5));
		}

		[Test]
		public void SimpleSubtraction()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int>("return 10 - 3;");
			Assert.That(func(), Is.EqualTo(7));
		}

		[Test]
		public void SimpleMultiplication()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int>("return 5 * 6;");
			Assert.That(func(), Is.EqualTo(30));
		}

		[Test]
		public void SimpleDivision()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int>("return 20 / 4;");
			Assert.That(func(), Is.EqualTo(5));
		}

		[Test]
		public void SimpleModulo()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int>("return 17 % 5;");
			Assert.That(func(), Is.EqualTo(2));
		}

		[Test]
		public void ComplexArithmetic()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int>("return 2 + 3 * 4 - 10 / 2;");
			Assert.That(func(), Is.EqualTo(9));
		}

		// Parameter tests
		[Test]
		public void SingleParameter()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, int>("return x + 4;", "x");
			Assert.That(func(6), Is.EqualTo(10));
		}

		[Test]
		public void ParameterMultiplication()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, int>("return x * 2;", "x");
			Assert.That(func(5), Is.EqualTo(10));
		}

		// If/else tests
		[Test]
		public void SimpleIfElse()
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

			Assert.That(func(true), Is.EqualTo(1));
			Assert.That(func(false), Is.EqualTo(0));
		}

		[Test]
		public void IfWithoutElse()
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

			Assert.DoesNotThrow(() => func(10));
			Assert.DoesNotThrow(() => func(3));
		}

		[Test]
		public void NestedIf()
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

			Assert.That(func(5), Is.EqualTo(0));
			Assert.That(func(15), Is.EqualTo(1));
			Assert.That(func(25), Is.EqualTo(2));
		}

		// Comparison operators
		[Test]
		public void LessThanComparison()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, bool>("return x < 10;", "x");
			Assert.That(func(5), Is.True);
			Assert.That(func(15), Is.False);
		}

		[Test]
		public void GreaterThanComparison()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, bool>("return x > 10;", "x");
			Assert.That(func(15), Is.True);
			Assert.That(func(5), Is.False);
		}

		[Test]
		public void EqualityComparison()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, bool>("return x == 10;", "x");
			Assert.That(func(10), Is.True);
			Assert.That(func(5), Is.False);
		}

		[Test]
		public void NotEqualComparison()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, bool>("return x != 10;", "x");
			Assert.That(func(5), Is.True);
			Assert.That(func(10), Is.False);
		}

		[Test]
		public void LessThanOrEqualComparison()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, bool>("return x <= 10;", "x");
			Assert.That(func(10), Is.True);
			Assert.That(func(5), Is.True);
			Assert.That(func(15), Is.False);
		}

		[Test]
		public void GreaterThanOrEqualComparison()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, bool>("return x >= 10;", "x");
			Assert.That(func(10), Is.True);
			Assert.That(func(15), Is.True);
			Assert.That(func(5), Is.False);
		}

		// Logical operators
		[Test]
		public void LogicalAnd()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, bool>("return x > 5 && x < 15;", "x");
			Assert.That(func(10), Is.True);
			Assert.That(func(3), Is.False);
			Assert.That(func(20), Is.False);
		}

		[Test]
		public void LogicalOr()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, bool>("return x < 5 || x > 15;", "x");
			Assert.That(func(3), Is.True);
			Assert.That(func(20), Is.True);
			Assert.That(func(10), Is.False);
		}

		[Test]
		public void LogicalNot()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<bool, bool>("return !x;", "x");
			Assert.That(func(true), Is.False);
			Assert.That(func(false), Is.True);
		}

		// Boolean literals
		[Test]
		public void BooleanLiteralTrue()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<bool>("return true;");
			Assert.That(func(), Is.True);
		}

		[Test]
		public void BooleanLiteralFalseVariable()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<bool>("bool b = false; return b;");
			Assert.That(func(), Is.False);
		}

		[Test]
		public void BooleanLiteralFlagPattern()
		{
			var compiler = new ExpressionMethodCompiler();
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
			var compiler = new ExpressionMethodCompiler();

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
			var compiler = new ExpressionMethodCompiler();

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
			var compiler = new ExpressionMethodCompiler();

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
			var compiler = new ExpressionMethodCompiler();

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
			var compiler = new ExpressionMethodCompiler();

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
			var compiler = new ExpressionMethodCompiler();

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
			var compiler = new ExpressionMethodCompiler();

			var func = compiler.CompileFunc<int>(
				$"""
				 int x = 5;
				 x--;
				 return x;
				 """);

			Assert.That(func(), Is.EqualTo(4));
		}

		// While loop tests
		[Test]
		public void SimpleWhileLoop()
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

			Assert.That(func(3), Is.EqualTo(126));
		}

		[Test]
		public void WhileLoopWithBreak()
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

			Assert.That(func(), Is.EqualTo(10));
		}

		// For loop tests
		[Test]
		public void SimpleForLoop()
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

			Assert.That(func(), Is.EqualTo(10));
		}

		[Test]
		public void ForLoopWithMultiplication()
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

			Assert.That(func(), Is.EqualTo(120));
		}

		// Ternary operator tests
		[Test]
		public void SimpleTernary()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, int>("return x > 10 ? 1 : 0;", "x");
			Assert.That(func(15), Is.EqualTo(1));
			Assert.That(func(5), Is.EqualTo(0));
		}

		[Test]
		public void NestedTernary()
		{
			var compiler = new ExpressionMethodCompiler();

			var func = compiler.CompileFunc<int, int>(
				"return x > 10 ? (x > 20 ? 2 : 1) : 0;",
				"x");

			Assert.That(func(5), Is.EqualTo(0));
			Assert.That(func(15), Is.EqualTo(1));
			Assert.That(func(25), Is.EqualTo(2));
		}

		// Local method tests
		[Test]
		public void LocalMethodDefinition()
		{
			var compiler = new ExpressionMethodCompiler();

			var func = compiler.CompileFunc<int, int>(
				$$"""
				  int twice(int x)
				  {
				      return x * 2;
				  }
				  return twice(x);
				  """,
				"x");

			Assert.That(func(5), Is.EqualTo(10));
		}

		[Test]
		public void MultipleLocalMethods()
		{
			var compiler = new ExpressionMethodCompiler();

			var func = compiler.CompileFunc<int, int>(
				$$"""
				  int add(int a, int b)
				  {
				      return a + b;
				  }
				  int multiply(int a, int b)
				  {
				      return a * b;
				  }
				  return add(multiply(x, 2), 5);
				  """,
				"x");

			Assert.That(func(3), Is.EqualTo(11));
		}

		// Registered method tests
		[Test]
		public void RegisteredStaticMethod()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterStaticMethods(typeof(Math));
			var func = compiler.CompileFunc<int, int>("return (int)Abs(x);", "x");
			Assert.That(func(-5), Is.EqualTo(5));
			Assert.That(func(5), Is.EqualTo(5));
		}

		// Complex integration tests
		[Test]
		public void FibonacciSequence()
		{
			var compiler = new ExpressionMethodCompiler();

			var func = compiler.CompileFunc<int, int>(
				$$"""
				  if (x <= 1)
				  {
				      return x;
				  }
				  int a = 0;
				  int b = 1;
				  for (int i = 2; i <= x; i++)
				  {
				      int temp = a + b;
				      a = b;
				      b = temp;
				  }
				  return b;
				  """,
				"x");

			Assert.That(func(0), Is.EqualTo(0));
			Assert.That(func(1), Is.EqualTo(1));
			Assert.That(func(6), Is.EqualTo(8));
			Assert.That(func(10), Is.EqualTo(55));
		}

		[Test]
		public void ComplexConditionalsAndLoops()
		{
			var compiler = new ExpressionMethodCompiler();

			var func = compiler.CompileFunc<int, int>(
				$$"""
				  int result = 0;
				  for (int i = 1; i <= x; i++)
				  {
				      if (i % 2 == 0)
				      {
				          result += i;
				      }
				      else
				      {
				          result -= i;
				      }
				  }
				  return result;
				  """,
				"x");

			Assert.That(func(5), Is.EqualTo(-3)); // -1 + 2 - 3 + 4 - 5 = -3
			Assert.That(func(10), Is.EqualTo(5)); // -1 + 2 - 3 + 4 - 5 + 6 - 7 + 8 - 9 + 10 = 5
		}

		[Test]
		public void MixedOperators()
		{
			var compiler = new ExpressionMethodCompiler();

			var func = compiler.CompileFunc<int, int>(
				$"""
				 int result = x;
				 result += 5;
				 result *= 2;
				 result++;
				 result -= 3;
				 return result;
				 """,
				"x");

			Assert.That(func(10), Is.EqualTo(28));
		}

		// Object construction tests
		[Test]
		public void CreateObjectWithNew()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterType(typeof(System.Text.StringBuilder));

			var func = compiler.CompileFunc<string>(
				$"""
				 var sb = new System.Text.StringBuilder();
				 return "created";
				 """);

			Assert.That(func(), Is.EqualTo("created"));
		}

		[Test]
		public void CreateObjectWithConstructorArguments()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterType(typeof(System.Text.StringBuilder));

			var func = compiler.CompileFunc<string>(
				$"""
				 var sb = new StringBuilder("Hello");
				 return "created";
				 """);

			Assert.That(func(), Is.EqualTo("created"));
		}

		// Member access tests
		[Test]
		public void AccessPropertyOnObject()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterType(typeof(System.Text.StringBuilder));

			var func = compiler.CompileFunc<int>(
				$"""
				 var sb = new System.Text.StringBuilder("Hello");
				 return sb.Length;
				 """);

			Assert.That(func(), Is.EqualTo(5));
		}

		[Test]
		public void CallMethodOnObject()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterType(typeof(System.Text.StringBuilder));

			var func = compiler.CompileFunc<string>(
				$"""
				 var sb = new System.Text.StringBuilder();
				 sb.Append("Hello");
				 sb.Append(" ");
				 sb.Append("World");
				 return sb.ToString();
				 """);

			Assert.That(func(), Is.EqualTo("Hello World"));
		}

		[Test]
		public void ModifyPropertyOnObject()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterType(typeof(TestVector3));

			var func = compiler.CompileFunc<double>(
				$"""
				 var v = new TestVector3(1.0, 2.0, 3.0);
				 v.x = 10.0;
				 return v.x;
				 """);

			Assert.That(func(), Is.EqualTo(10.0));
		}

		// LINQ extension method tests
		[Test]
		public void LinqWhereOnList()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterStaticMethods(typeof(Enumerable));

			var func = compiler.CompileFunc<List<int>, int>(
				$"""
				 var filtered = list.Where(x => x > 5);
				 return filtered.Count();
				 """,
				"list");

			var testList = new List<int>
			{
				1,
				3,
				5,
				7,
				9,
				11
			};

			Assert.That(func(testList), Is.EqualTo(3));
		}

		[Test]
		public void LinqSelectOnList()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterStaticMethods(typeof(Enumerable));

			var func = compiler.CompileFunc<List<int>, int>(
				$"""
				 var doubled = list.Select(x => x * 2);
				 return doubled.First();
				 """,
				"list");

			var testList = new List<int>
			{
				5, 10, 15
			};

			Assert.That(func(testList), Is.EqualTo(10));
		}

		[Test]
		public void LinqChainedOperations()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterStaticMethods(typeof(Enumerable));

			var func = compiler.CompileFunc<List<int>, int>(
				$"""
				 var result = list.Where(x => x > 3).Select(x => x * 2).Sum();
				 return result;
				 """,
				"list");

			var testList = new List<int>
			{
				1,
				2,
				3,
				4,
				5
			};

			Assert.That(func(testList), Is.EqualTo(18)); // (4 * 2) + (5 * 2) = 18
		}

		// Complex Unity-style tests
		[Test]
		public void Vector3Construction()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterType(typeof(TestVector3), "Vector3");

			var func = compiler.CompileFunc<double>(
				$"""
				 var v = new Vector3(1.0, 2.0, 3.0);
				 return v.x + v.y + v.z;
				 """);

			Assert.That(func(), Is.EqualTo(6.0));
		}

		[Test]
		public void Vector3PropertyModification()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterType(typeof(TestVector3), "Vector3");

			var func = compiler.CompileFunc<TestVector3, double>(
				$"""
				 v.x = v.x + 10.0;
				 v.y = v.y * 2.0;
				 return v.x + v.y;
				 """,
				"v");

			var testVector = new TestVector3(5.0, 3.0, 0.0);
			Assert.That(func(testVector), Is.EqualTo(21.0)); // (5 + 10) + (3 * 2) = 21
		}

		[Test]
		public void TransformPositionManipulation()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterType(typeof(TestTransform), "Transform");
			compiler.RegisterType(typeof(TestVector3), "Vector3");

			var func = compiler.CompileAction<TestTransform>(
				$"""
				 var newPos = new Vector3(10.0, 20.0, 30.0);
				 transform.position = newPos;
				 """,
				"transform");

			var testTransform = new TestTransform();
			func(testTransform);

			Assert.That(testTransform.position.x, Is.EqualTo(10.0));
			Assert.That(testTransform.position.y, Is.EqualTo(20.0));
			Assert.That(testTransform.position.z, Is.EqualTo(30.0));
		}

		[Test]
		public void TransformPositionComponentAccess()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterType(typeof(TestTransform), "Transform");
			compiler.RegisterType(typeof(TestVector3), "Vector3");

			var func = compiler.CompileFunc<TestTransform, double>(
				$"""
				 transform.position.x = 100.0;
				 return transform.position.x;
				 """,
				"transform");

			var testTransform = new TestTransform();
			testTransform.position = new TestVector3(1.0, 2.0, 3.0);

			var result = func(testTransform);
			Assert.That(result, Is.EqualTo(100.0));
		}

		[Test]
		public void ComplexObjectManipulation()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterType(typeof(TestTransform), "Transform");
			compiler.RegisterType(typeof(TestVector3), "Vector3");

			var func = compiler.CompileFunc<TestTransform, double>(
				$"""
				 var offset = new Vector3(5.0, 10.0, 15.0);
				 transform.position.x = transform.position.x + offset.x;
				 transform.position.y = transform.position.y + offset.y;
				 return transform.position.x + transform.position.y;
				 """,
				"transform");

			var testTransform = new TestTransform();
			testTransform.position = new TestVector3(1.0, 2.0, 3.0);

			var result = func(testTransform);
			Assert.That(result, Is.EqualTo(18.0)); // (1 + 5) + (2 + 10) = 18
		}

		[Test]
		public void ComprehensiveIntegrationTest()
		{
			// This test combines LINQ operations with complex object manipulation
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterStaticMethods(typeof(Enumerable));
			compiler.RegisterType(typeof(TestVector3), "Vector3");

			var func = compiler.CompileFunc<List<TestVector3>, double>(
				$"""
				 // Filter vectors where x > 0, get their y components, and sum them
				 var result = vectors.Where(v => v.x > 0.0).Select(v => v.y).Sum();

				 // Create a new vector and add its magnitude to the result
				 var newVec = new Vector3(3.0, 4.0, 0.0);
				 result = result + newVec.x + newVec.y;

				 return result;
				 """,
				"vectors");

			List<TestVector3> testVectors = new()
			{
				new(-1.0, 10.0, 0.0), // Filtered out (x <= 0)
				new(1.0, 20.0, 0.0), // y = 20
				new(2.0, 30.0, 0.0), // y = 30
				new(0.0, 5.0, 0.0)
			};

			// Expected: (20 + 30) + (3 + 4) = 57
			Assert.That(func(testVectors), Is.EqualTo(57.0));
		}

		[Test]
		public void LocalFunctionLinq()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterStaticMethods(typeof(Enumerable));

			var func = compiler.CompileFunc<List<int>, int>(
				$$"""
				  int square(int x)
				  {
				      return x * x;
				  }
				  var squares = list.Select(x => square(x));
				  return squares.Sum();
				  """,
				"list");

			var testList = new List<int>
			{
				1,
				2,
				3,
				4,
				5
			};

			Assert.That(func(testList), Is.EqualTo(55));
		}
		
		[Test]
		public void ComplexListParameterTest()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterStaticMethods(typeof(Enumerable));
			compiler.RegisterStaticMethods(typeof(Mathf));
			compiler.RegisterType(typeof(Vector3));

			var func = compiler.CompileFunc<List<Vector3>, Vector3>(
				$$"""
				  float halfArena = 6.6f;
				  float cell = 0.6f;
				  int attempts = 0;
				  float x = 0f;
				  float y = 0f;
				  while (attempts < 50)
				  {
				      x = Round(halfArena / cell) * cell;
				      y = Round(halfArena / cell) * cell;
				      float fx = x;
				      float fy = y;
				      bool clash = taken.Any(p => p.x == fx && p.y == fy);
				      if (!clash) { return new Vector3(x, y, 0f); }
				      attempts++;
				  }
				  return new Vector3(x, y, 0f);
				  """,
				"taken");

			var vector3 = func(new List<Vector3>());
			Assert.That(vector3.x, Is.EqualTo(6.6f).Within(0.01f));
			Assert.That(vector3.y, Is.EqualTo(6.6f).Within(0.01f));
			Assert.That(vector3.z, Is.EqualTo(0f).Within(0.01f));
		}

		// Compound assignment operators (multiply / divide)
		[Test]
		public void MultiplyAssignment()
		{
			var compiler = new ExpressionMethodCompiler();

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
			var compiler = new ExpressionMethodCompiler();

			var func = compiler.CompileFunc<int, int>(
				$"""
				 int result = 20;
				 result /= x;
				 return result;
				 """,
				"x");

			Assert.That(func(4), Is.EqualTo(5));
		}

		// continue
		[Test]
		public void ContinueStatement()
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

			Assert.That(func(), Is.EqualTo(9)); // 1 + 3 + 5
		}

		// Float literal arithmetic with the 'f' suffix
		[Test]
		public void FloatLiteralArithmetic()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<float>("return 1.5f + 2.5f;");
			Assert.That(func(), Is.EqualTo(4f).Within(0.0001f));
		}

		// Explicit numeric casts
		[Test]
		public void CastToDouble()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<double>("return (double)7 / (double)2;");
			Assert.That(func(), Is.EqualTo(3.5));
		}

		[Test]
		public void CastToFloat()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<float>("return (float)9 / (float)2;");
			Assert.That(func(), Is.EqualTo(4.5f).Within(0.0001f));
		}

		// LINQ Last()
		[Test]
		public void LinqLastOnList()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterStaticMethods(typeof(Enumerable));

			var func = compiler.CompileFunc<List<int>, int>(
				$"""
				 return list.Last();
				 """,
				"list");

			var testList = new List<int>
			{
				5, 10, 15
			};

			Assert.That(func(testList), Is.EqualTo(15));
		}

		// String escapes are not interpreted: the lexer only strips the backslash.
		[Test]
		public void StringEscapesAreNotInterpreted()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<string>("return \"a\\nb\";");
			Assert.That(func(), Is.EqualTo("anb"));
		}

		// Implicit numeric promotion in binary operations (issue #73)
		[Test]
		public void FloatPlusIntPromotes()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<float, int, float>("return x + y;", "x", "y");
			Assert.That(func(1.5f, 2), Is.EqualTo(3.5f).Within(0.0001f));
		}

		[Test]
		public void IntPlusFloatPromotes()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, float, float>("return x + y;", "x", "y");
			Assert.That(func(2, 1.5f), Is.EqualTo(3.5f).Within(0.0001f));
		}

		[Test]
		public void IntMinusFloatPromotes()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, float, float>("return x - y;", "x", "y");
			Assert.That(func(5, 1.5f), Is.EqualTo(3.5f).Within(0.0001f));
		}

		[Test]
		public void IntTimesFloatPromotes()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, float, float>("return x * y;", "x", "y");
			Assert.That(func(3, 2.5f), Is.EqualTo(7.5f).Within(0.0001f));
		}

		[Test]
		public void IntDividedByFloatPromotes()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, float, float>("return x / y;", "x", "y");
			Assert.That(func(5, 2f), Is.EqualTo(2.5f).Within(0.0001f));
		}

		[Test]
		public void FloatModuloIntPromotes()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<float, int, float>("return x % y;", "x", "y");
			Assert.That(func(5.5f, 2), Is.EqualTo(1.5f).Within(0.0001f));
		}

		[Test]
		public void IntPlusDoublePromotes()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, double, double>("return x + y;", "x", "y");
			Assert.That(func(2, 1.5), Is.EqualTo(3.5).Within(0.0001));
		}

		[Test]
		public void MixedLessThanComparison()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<float, int, bool>("return x < y;", "x", "y");
			Assert.That(func(1.5f, 2), Is.True);
			Assert.That(func(2.5f, 2), Is.False);
		}

		[Test]
		public void MixedGreaterThanOrEqualComparison()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int, float, bool>("return x >= y;", "x", "y");
			Assert.That(func(3, 2.5f), Is.True);
			Assert.That(func(2, 2.5f), Is.False);
		}

		[Test]
		public void MixedEqualityComparison()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<float, int, bool>("return x == y;", "x", "y");
			Assert.That(func(2f, 2), Is.True);
			Assert.That(func(2.5f, 2), Is.False);
		}

		[Test]
		public void FloatVariablePlusEqualsInt()
		{
			var compiler = new ExpressionMethodCompiler();
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
			var compiler = new ExpressionMethodCompiler();
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
			var compiler = new ExpressionMethodCompiler();
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
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<float, int>(
				$$"""
				int total = 5;
				total += x;
				return total;
				""",
				"x");
			Assert.That(func(2.9f), Is.EqualTo(7));
		}

		// --- Cross-expression calls (issue #72) ---

		[Test]
		public void RegisteredExpressionCanBeCalledByAnother()
		{
			var compiler = new ExpressionMethodCompiler();

			Func<int, int, int> add = (a, b) => a + b;
			compiler.RegisterExpression("add", add, new[] { typeof(int), typeof(int) }, typeof(int));

			var func = compiler.CompileFunc<int, int>("return add(x, 10);", "x");

			Assert.That(func(5), Is.EqualTo(15));
		}

		[Test]
		public void RegisteredExpressionCallsAreNested()
		{
			var compiler = new ExpressionMethodCompiler();

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
			var compiler = new ExpressionMethodCompiler();

			Func<float, float> half = v => v * 0.5f;
			compiler.RegisterExpression("half", half, new[] { typeof(float) }, typeof(float));

			// Passes an int literal where the callee expects a float.
			var func = compiler.CompileFunc<float>("return half(10);");

			Assert.That(func(), Is.EqualTo(5f).Within(0.001f));
		}

		// --- ^ (XOR) operator ---

		[Test]
		public void BooleanXor()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<bool, bool, bool>("return a ^ b;", "a", "b");

			Assert.That(func(true, false), Is.True);
			Assert.That(func(true, true), Is.False);
			Assert.That(func(false, false), Is.False);
		}

		[Test]
		public void IntegerXor()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<int>("return 6 ^ 3;");

			Assert.That(func(), Is.EqualTo(5));
		}

		[Test]
		public void XorBindsLooserThanEqualityAndTighterThanLogicalAnd()
		{
			var compiler = new ExpressionMethodCompiler();

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
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<Vector3, Vector3>("return -v;", "v");

			Assert.That(func(new Vector3(1, -2, 3)), Is.EqualTo(new Vector3(-1, 2, -3)));
		}

		[Test]
		public void Vector3Addition()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<Vector3, Vector3, Vector3>("return a + b;", "a", "b");

			Assert.That(func(new Vector3(1, 2, 3), new Vector3(4, 5, 6)), Is.EqualTo(new Vector3(5, 7, 9)));
		}

		[Test]
		public void Vector3MultiplyByFloatScalar()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<Vector3, Vector3>("return v * 2f;", "v");

			Assert.That(func(new Vector3(1, 2, 3)), Is.EqualTo(new Vector3(2, 4, 6)));
		}

		[Test]
		public void Vector3MultiplyByIntScalarPromotesToFloat()
		{
			var compiler = new ExpressionMethodCompiler();
			// `2` lexes to int; the vector operator takes a float, so the scalar must widen.
			var func = compiler.CompileFunc<Vector3, Vector3>("return v * 2;", "v");

			Assert.That(func(new Vector3(1, 2, 3)), Is.EqualTo(new Vector3(2, 4, 6)));
		}

		[Test]
		public void Vector3DivideByScalar()
		{
			var compiler = new ExpressionMethodCompiler();
			var func = compiler.CompileFunc<Vector3, Vector3>("return v / 2;", "v");

			Assert.That(func(new Vector3(2, 4, 6)), Is.EqualTo(new Vector3(1, 2, 3)));
		}
	}

	public class TestVector3
	{
		public double x;
		public double y;
		public double z;

		public TestVector3(double x, double y, double z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}
	}

	public class TestTransform
	{
		public TestVector3 position = new(0, 0, 0);
	}
}