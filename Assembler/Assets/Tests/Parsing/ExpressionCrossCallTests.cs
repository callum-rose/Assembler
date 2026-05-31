using System;
using System.Collections.Generic;
using Assembler.Compiler.Compiler;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;

namespace Tests.Parsing
{
	public class ExpressionCrossCallTests
	{
		private static ExpressionInfo Expr(string id, string returnType, string expression,
			IReadOnlyList<(string type, string name)>? args = null, string? callableAs = null) =>
			new(id,
				args ?? Array.Empty<(string, string)>(),
				returnType,
				Array.Empty<string>(),
				Array.Empty<string>(),
				expression,
				callableAs);

		private static CompiledExpressionsRegistry NewRegistry() =>
			new(BuiltInTypeRegistry.Default, new ExpressionMethodCompiler());

		[Test]
		public void ExpressionCanCallAnotherByCamelCasedId()
		{
			var registry = NewRegistry();

			registry.CompileAndRegisterAll(new[]
			{
				Expr("base offset", "int", "return a + b;", new[] { ("int", "a"), ("int", "b") }),
				Expr("use offset", "int", "return baseOffset(x, 3);", new[] { ("int", "x") }),
			});

			var func = (Func<int, int>)registry.GetCompiled("use offset").@delegate;

			Assert.That(func(4), Is.EqualTo(7));
		}

		[Test]
		public void OrderingIsResolvedRegardlessOfDeclarationOrder()
		{
			var registry = NewRegistry();

			// Caller is declared before callee; topo sort must reorder compilation.
			registry.CompileAndRegisterAll(new[]
			{
				Expr("use offset", "int", "return baseOffset(x, 3);", new[] { ("int", "x") }),
				Expr("base offset", "int", "return a + b;", new[] { ("int", "a"), ("int", "b") }),
			});

			var func = (Func<int, int>)registry.GetCompiled("use offset").@delegate;

			Assert.That(func(4), Is.EqualTo(7));
		}

		[Test]
		public void NestedExpressionCallsResolve()
		{
			var registry = NewRegistry();

			registry.CompileAndRegisterAll(new[]
			{
				Expr("top", "int", "return middle(n) + 1;", new[] { ("int", "n") }),
				Expr("middle", "int", "return bottom(n) * 2;", new[] { ("int", "n") }),
				Expr("bottom", "int", "return n + 10;", new[] { ("int", "n") }),
			});

			var func = (Func<int, int>)registry.GetCompiled("top").@delegate;

			// bottom(5)=15, middle=30, top=31
			Assert.That(func(5), Is.EqualTo(31));
		}

		[Test]
		public void ExplicitAliasOverridesCamelCasedName()
		{
			var registry = NewRegistry();

			registry.CompileAndRegisterAll(new[]
			{
				Expr("base offset", "int", "return a + b;", new[] { ("int", "a"), ("int", "b") },
					callableAs: "bo"),
				Expr("use offset", "int", "return bo(x, 3);", new[] { ("int", "x") }),
			});

			var func = (Func<int, int>)registry.GetCompiled("use offset").@delegate;

			Assert.That(func(4), Is.EqualTo(7));
		}

		[Test]
		public void CallableNameCollisionThrows()
		{
			var registry = NewRegistry();

			var ex = Assert.Throws<Exception>(() => registry.CompileAndRegisterAll(new[]
			{
				Expr("base offset", "int", "return 1;"),
				Expr("base_offset", "int", "return 2;"),
			}));

			Assert.That(ex!.Message, Does.Contain("collision"));
		}

		[Test]
		public void CyclicDependencyThrows()
		{
			var registry = NewRegistry();

			var ex = Assert.Throws<Exception>(() => registry.CompileAndRegisterAll(new[]
			{
				Expr("ping", "int", "return pong(n);", new[] { ("int", "n") }),
				Expr("pong", "int", "return ping(n);", new[] { ("int", "n") }),
			}));

			Assert.That(ex!.Message, Does.Contain("Cyclic"));
		}
	}
}
