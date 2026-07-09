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
	/// Parameter, local-method, registered-method, object-construction, member-access,
	/// type-in-declaration-position and local-method-signature tests.
	/// </summary>
	public class MethodAndTypeRegistrationTests : CompilerTestBase
	{
		// Parameter tests
		[Test]
		public void SingleParameter()
		{
			var func = compiler.CompileFunc<int, int>("return x + 4;", "x");
			Assert.That(func(6), Is.EqualTo(10));
		}

		[Test]
		public void ParameterMultiplication()
		{
			var func = compiler.CompileFunc<int, int>("return x * 2;", "x");
			Assert.That(func(5), Is.EqualTo(10));
		}

		// Local method tests
		[Test]
		public void LocalMethodDefinition()
		{

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
			compiler.RegisterStaticMethods(typeof(Math));
			var func = compiler.CompileFunc<int, int>("return (int)Abs(x);", "x");
			Assert.That(func(-5), Is.EqualTo(5));
			Assert.That(func(5), Is.EqualTo(5));
		}

		// A registered static method is callable by its type-qualified name (`Mathf.Sin(x)`), as in regular
		// C#, not only by its bare name. RegisterStaticMethods registers the type so the qualified form
		// resolves through the static-member-access path (issue #402).
		[Test]
		public void RegisteredStaticMethodCallableByTypeQualifiedName()
		{
			compiler.RegisterStaticMethods(typeof(Mathf));
			var func = compiler.CompileFunc<float, float>("return Mathf.Sin(x);", "x");
			Assert.That(func(0f), Is.EqualTo(0f).Within(1e-4f));
			Assert.That(func(Mathf.PI / 2f), Is.EqualTo(1f).Within(1e-4f));
		}

		// The fully-qualified name (`UnityEngine.Mathf.Sin(x)`) also resolves.
		[Test]
		public void RegisteredStaticMethodCallableByFullyQualifiedName()
		{
			compiler.RegisterStaticMethods(typeof(Mathf));
			var func = compiler.CompileFunc<float, float>("return UnityEngine.Mathf.Sin(x);", "x");
			Assert.That(func(Mathf.PI / 2f), Is.EqualTo(1f).Within(1e-4f));
		}

		// The bare-name form keeps working alongside the qualified form.
		[Test]
		public void RegisteredStaticMethodStillCallableByBareName()
		{
			compiler.RegisterStaticMethods(typeof(Mathf));
			var func = compiler.CompileFunc<float, float>("return Sin(x);", "x");
			Assert.That(func(Mathf.PI / 2f), Is.EqualTo(1f).Within(1e-4f));
		}

		// Object construction tests
		[Test]
		public void CreateObjectWithNew()
		{
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
			compiler.RegisterType(typeof(TestVector3));

			var func = compiler.CompileFunc<double>(
				$"""
				 var v = new TestVector3(1.0, 2.0, 3.0);
				 v.x = 10.0;
				 return v.x;
				 """);

			Assert.That(func(), Is.EqualTo(10.0));
		}

		// A registered type name may be used in declaration position, just like `var` or a primitive keyword.
		[Test]
		public void RegisteredTypeVariableDeclaration()
		{
			compiler.RegisterType(typeof(TestVector3));

			var func = compiler.CompileFunc<double>(
				$"""
				 TestVector3 v = new TestVector3(1.0, 2.0, 3.0);
				 return v.x + v.y;
				 """);

			Assert.That(func(), Is.EqualTo(3.0));
		}

		// A fully-qualified type name works in declaration position too.
		[Test]
		public void FullyQualifiedTypeVariableDeclaration()
		{
			compiler.RegisterType(typeof(Vector3));

			var func = compiler.Compile(
				$"""
				 UnityEngine.Vector3 dir = new UnityEngine.Vector3(1f, 2f, 3f);
				 return dir;
				 """,
				typeof(Vector3),
				out _);

			Assert.That(func.DynamicInvoke(), Is.EqualTo(new Vector3(1f, 2f, 3f)));
		}

		// Assignment to a member must still parse as an expression statement, not a declaration.
		[Test]
		public void MemberAssignmentNotMisreadAsDeclaration()
		{
			compiler.RegisterType(typeof(TestVector3));

			var func = compiler.CompileFunc<double>(
				$"""
				 var v = new TestVector3(1.0, 2.0, 3.0);
				 v.x = 5.0;
				 return v.x;
				 """);

			Assert.That(func(), Is.EqualTo(5.0));
		}

		// --- Typo'd namespace/type reports "Unknown identifier", not the ConstantExpression fallback ---

		[Test]
		public void TypoedStaticCallReportsUnknownIdentifier()
		{
			var ex = Assert.Throws<CompileException>(() => compiler.CompileFunc<float>("return Mthf.Abs(-3f);"));
			Assert.That(ex.Message, Does.Contain("Unknown identifier"));
			Assert.That(ex.Message, Does.Contain("Mthf"));
			Assert.That(ex.Message, Does.Not.Contain("ConstantExpression"));
			Assert.That(ex.Line, Is.GreaterThan(0));
		}

		[Test]
		public void TypoedBareDottedNameReportsUnknownIdentifier()
		{
			var ex = Assert.Throws<CompileException>(() => compiler.CompileFunc<float>("return Mthf.Pi;"));
			Assert.That(ex.Message, Does.Contain("Unknown identifier"));
			Assert.That(ex.Message, Does.Contain("Mthf"));
		}

		// --- Local-method signatures: positioned errors, and registered types now usable as parameters ---

		[Test]
		public void LocalMethodUnknownParameterTypeIsAPositionedCompileError()
		{
			var ex = Assert.Throws<CompileException>(
				() => compiler.CompileFunc<int>("int f(Bogus b) { return 1; }\nreturn f(0);"));
			Assert.That(ex.Message, Does.Contain("Bogus"));
			Assert.That(ex.Message, Does.Contain("not found"));
			Assert.That(ex.Line, Is.GreaterThan(0));
		}

		[Test]
		public void LocalMethodRegisteredParameterTypeResolves()
		{
			compiler.RegisterType(typeof(Vector3));

			var func = compiler.CompileFunc<float>("float f(Vector3 v) { return v.x; }\nreturn f(new Vector3(7f, 0f, 0f));");

			Assert.That(func(), Is.EqualTo(7f).Within(1e-4f));
		}

		[Test]
		public void LocalMethodMissingClosingBraceIsAPositionedCompileError()
		{
			var ex = Assert.Throws<CompileException>(
				() => compiler.CompileFunc<int>("int f() { return 1;\nreturn f();"));
			Assert.That(ex.Message, Does.Contain("Unbalanced braces"));
			Assert.That(ex.Line, Is.GreaterThan(0));
		}
	}
}
