using System;
using Assembler.Compiler.Compiler;
using UnityEngine;

namespace Spike.CompilerHarness.Cases
{
	/// <summary>
	/// Port of <c>Assets/Tests/Compiler/MethodAndTypeRegistrationTests.cs</c> (21 cases): parameter,
	/// local-method, registered-method, object-construction, member-access, type-in-declaration-position
	/// and local-method-signature cases. Bodies are copied verbatim from the source suite. The
	/// registration paths lean on reflection, which is the other thing IL2CPP can strip out from under us.
	/// </summary>
	public static class MethodAndTypeRegistrationCases
	{
		private const string Suite = "MethodAndTypeRegistration";

		public static void Register(SpikeCaseList list)
		{
			list.Add($"{Suite}/SingleParameter", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, int>("return x + 4;", "x");
				Check.Equal(func(6), 10);
			});

			list.Add($"{Suite}/ParameterMultiplication", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var func = compiler.CompileFunc<int, int>("return x * 2;", "x");
				Check.Equal(func(5), 10);
			});

			list.Add($"{Suite}/LocalMethodDefinition", () =>
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

				Check.Equal(func(5), 10);
			});

			list.Add($"{Suite}/MultipleLocalMethods", () =>
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

				Check.Equal(func(3), 11);
			});

			list.Add($"{Suite}/RegisteredStaticMethod", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterStaticMethods(typeof(Math));
				var func = compiler.CompileFunc<int, int>("return (int)Abs(x);", "x");
				Check.Equal(func(-5), 5);
				Check.Equal(func(5), 5);
			});

			list.Add($"{Suite}/RegisteredStaticMethodCallableByTypeQualifiedName", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterStaticMethods(typeof(Mathf));
				var func = compiler.CompileFunc<float, float>("return Mathf.Sin(x);", "x");
				Check.Approx(func(0f), 0f, 1e-4f);
				Check.Approx(func(Mathf.PI / 2f), 1f, 1e-4f);
			});

			list.Add($"{Suite}/RegisteredStaticMethodCallableByFullyQualifiedName", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterStaticMethods(typeof(Mathf));
				var func = compiler.CompileFunc<float, float>("return UnityEngine.Mathf.Sin(x);", "x");
				Check.Approx(func(Mathf.PI / 2f), 1f, 1e-4f);
			});

			list.Add($"{Suite}/RegisteredStaticMethodStillCallableByBareName", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterStaticMethods(typeof(Mathf));
				var func = compiler.CompileFunc<float, float>("return Sin(x);", "x");
				Check.Approx(func(Mathf.PI / 2f), 1f, 1e-4f);
			});

			list.Add($"{Suite}/CreateObjectWithNew", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(System.Text.StringBuilder));

				var func = compiler.CompileFunc<string>(
					$"""
					 var sb = new System.Text.StringBuilder();
					 return "created";
					 """);

				Check.Equal(func(), "created");
			});

			list.Add($"{Suite}/CreateObjectWithConstructorArguments", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(System.Text.StringBuilder));

				var func = compiler.CompileFunc<string>(
					$"""
					 var sb = new StringBuilder("Hello");
					 return "created";
					 """);

				Check.Equal(func(), "created");
			});

			list.Add($"{Suite}/AccessPropertyOnObject", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(System.Text.StringBuilder));

				var func = compiler.CompileFunc<int>(
					$"""
					 var sb = new System.Text.StringBuilder("Hello");
					 return sb.Length;
					 """);

				Check.Equal(func(), 5);
			});

			list.Add($"{Suite}/CallMethodOnObject", () =>
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

				Check.Equal(func(), "Hello World");
			});

			list.Add($"{Suite}/ModifyPropertyOnObject", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(TestVector3));

				var func = compiler.CompileFunc<double>(
					$"""
					 var v = new TestVector3(1.0, 2.0, 3.0);
					 v.x = 10.0;
					 return v.x;
					 """);

				Check.Equal(func(), 10.0);
			});

			list.Add($"{Suite}/RegisteredTypeVariableDeclaration", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(TestVector3));

				var func = compiler.CompileFunc<double>(
					$"""
					 TestVector3 v = new TestVector3(1.0, 2.0, 3.0);
					 return v.x + v.y;
					 """);

				Check.Equal(func(), 3.0);
			});

			list.Add($"{Suite}/FullyQualifiedTypeVariableDeclaration", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(Vector3));

				var func = compiler.Compile(
					$"""
					 UnityEngine.Vector3 dir = new UnityEngine.Vector3(1f, 2f, 3f);
					 return dir;
					 """,
					typeof(Vector3),
					out _);

				Check.Equal(func.DynamicInvoke(), new Vector3(1f, 2f, 3f));
			});

			list.Add($"{Suite}/MemberAssignmentNotMisreadAsDeclaration", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(TestVector3));

				var func = compiler.CompileFunc<double>(
					$"""
					 var v = new TestVector3(1.0, 2.0, 3.0);
					 v.x = 5.0;
					 return v.x;
					 """);

				Check.Equal(func(), 5.0);
			});

			list.Add($"{Suite}/TypoedStaticCallReportsUnknownIdentifier", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var ex = Check.ThrowsCompile(() => compiler.CompileFunc<float>("return Mthf.Abs(-3f);"),
					"typo'd static call");
				Check.Contains(ex.Message, "Unknown identifier", "message");
				Check.Contains(ex.Message, "Mthf", "message");
				Check.DoesNotContain(ex.Message, "ConstantExpression", "message");
				Check.Greater(ex.Line, 0, "line");
			});

			list.Add($"{Suite}/TypoedBareDottedNameReportsUnknownIdentifier", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var ex = Check.ThrowsCompile(() => compiler.CompileFunc<float>("return Mthf.Pi;"),
					"typo'd bare dotted name");
				Check.Contains(ex.Message, "Unknown identifier", "message");
				Check.Contains(ex.Message, "Mthf", "message");
			});

			list.Add($"{Suite}/LocalMethodUnknownParameterTypeIsAPositionedCompileError", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var ex = Check.ThrowsCompile(
					() => compiler.CompileFunc<int>("int f(Bogus b) { return 1; }\nreturn f(0);"),
					"local method with unknown parameter type");
				Check.Contains(ex.Message, "Bogus", "message");
				Check.Contains(ex.Message, "not found", "message");
				Check.Greater(ex.Line, 0, "line");
			});

			list.Add($"{Suite}/LocalMethodRegisteredParameterTypeResolves", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(Vector3));

				var func = compiler.CompileFunc<float>("float f(Vector3 v) { return v.x; }\nreturn f(new Vector3(7f, 0f, 0f));");

				Check.Approx(func(), 7f, 1e-4f);
			});

			list.Add($"{Suite}/LocalMethodMissingClosingBraceIsAPositionedCompileError", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				var ex = Check.ThrowsCompile(
					() => compiler.CompileFunc<int>("int f() { return 1;\nreturn f();"),
					"local method missing closing brace");
				Check.Contains(ex.Message, "Unbalanced braces", "message");
				Check.Greater(ex.Line, 0, "line");
			});
		}
	}
}
