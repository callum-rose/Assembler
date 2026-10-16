using System.Collections.Generic;
using System.Linq;
using Assembler.Compiler.Compiler;
using UnityEngine;

namespace Spike.CompilerHarness.Cases
{
	/// <summary>
	/// Port of <c>Assets/Tests/Compiler/LinqTests.cs</c> (15 cases): LINQ plus the larger
	/// integration / Unity-style cases that exercise several features at once. Bodies are copied verbatim
	/// from the source suite. <c>ComplexListParameterTest</c> is the closest the ported corpus gets to the
	/// value-type-generic failure mode the adversarial suite attacks head-on.
	/// </summary>
	public static class LinqCases
	{
		private const string Suite = "Linq";

		public static void Register(SpikeCaseList list)
		{
			list.Add($"{Suite}/FibonacciSequence", () =>
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

				Check.Equal(func(0), 0);
				Check.Equal(func(1), 1);
				Check.Equal(func(6), 8);
				Check.Equal(func(10), 55);
			});

			list.Add($"{Suite}/ComplexConditionalsAndLoops", () =>
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

				Check.Equal(func(5), -3); // -1 + 2 - 3 + 4 - 5 = -3
				Check.Equal(func(10), 5); // -1 + 2 - 3 + 4 - 5 + 6 - 7 + 8 - 9 + 10 = 5
			});

			list.Add($"{Suite}/MixedOperators", () =>
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

				Check.Equal(func(10), 28);
			});

			list.Add($"{Suite}/LinqWhereOnList", () =>
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

				Check.Equal(func(testList), 3);
			});

			list.Add($"{Suite}/LinqSelectOnList", () =>
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

				Check.Equal(func(testList), 10);
			});

			list.Add($"{Suite}/LinqChainedOperations", () =>
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

				Check.Equal(func(testList), 18); // (4 * 2) + (5 * 2) = 18
			});

			list.Add($"{Suite}/Vector3Construction", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(TestVector3), "Vector3");

				var func = compiler.CompileFunc<double>(
					$"""
					 var v = new Vector3(1.0, 2.0, 3.0);
					 return v.x + v.y + v.z;
					 """);

				Check.Equal(func(), 6.0);
			});

			list.Add($"{Suite}/Vector3PropertyModification", () =>
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
				Check.Equal(func(testVector), 21.0); // (5 + 10) + (3 * 2) = 21
			});

			list.Add($"{Suite}/TransformPositionManipulation", () =>
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

				Check.Equal(testTransform.position.x, 10.0, "position.x");
				Check.Equal(testTransform.position.y, 20.0, "position.y");
				Check.Equal(testTransform.position.z, 30.0, "position.z");
			});

			list.Add($"{Suite}/TransformPositionComponentAccess", () =>
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
				Check.Equal(result, 100.0);
			});

			list.Add($"{Suite}/ComplexObjectManipulation", () =>
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
				Check.Equal(result, 18.0); // (1 + 5) + (2 + 10) = 18
			});

			list.Add($"{Suite}/ComprehensiveIntegrationTest", () =>
			{
				// This case combines LINQ operations with complex object manipulation
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
				Check.Equal(func(testVectors), 57.0);
			});

			list.Add($"{Suite}/LocalFunctionLinq", () =>
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

				Check.Equal(func(testList), 55);
			});

			list.Add($"{Suite}/ComplexListParameterTest", () =>
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
				Check.Approx(vector3.x, 6.6f, 0.01f, "x");
				Check.Approx(vector3.y, 6.6f, 0.01f, "y");
				Check.Approx(vector3.z, 0f, 0.01f, "z");
			});

			list.Add($"{Suite}/LinqLastOnList", () =>
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

				Check.Equal(func(testList), 15);
			});
		}
	}
}
