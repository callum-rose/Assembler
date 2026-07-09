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
	/// LINQ and larger integration/Unity-style tests that exercise several features at once.
	/// </summary>
	public class LinqTests : CompilerTestBase
	{
		// Complex integration tests
		[Test]
		public void FibonacciSequence()
		{

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

		// LINQ extension method tests
		[Test]
		public void LinqWhereOnList()
		{
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

		// LINQ Last()
		[Test]
		public void LinqLastOnList()
		{
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
	}
}
