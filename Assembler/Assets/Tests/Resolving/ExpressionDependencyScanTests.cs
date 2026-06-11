using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Compiler.Compiler;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;

namespace Tests.Resolving
{
	// Covers the cross-expression dependency scan that orders expressions for compilation. The scan walks
	// lexer tokens rather than matching a regex over the raw source, so names appearing only in comments or
	// string literals — or as member calls like `xs.Count()` — must not register as dependencies.
	public class ExpressionDependencyScanTests
	{
		private static ExpressionInfo Float(string id, string body) =>
			new(id,
				Array.Empty<(string type, string name)>(),
				"float",
				Array.Empty<string>(),
				Array.Empty<string>(),
				body);

		private static CompiledExpressionsRegistry NewRegistry() =>
			new(BuiltInTypeRegistry.Default, new ExpressionMethodCompiler());

		[Test]
		public void NamesInCommentsDoNotCreateDependencies()
		{
			// Each body references the other's callable name only inside a comment. A source-text scan would
			// see a mutual dependency and abort with a spurious cycle; a token walk ignores comments.
			var expressions = new List<ExpressionInfo>
			{
				Float("alpha", "// beta() is unrelated\nreturn 1f;"),
				Float("beta", "// alpha() is unrelated\nreturn 2f;")
			};

			Assert.DoesNotThrow(() => NewRegistry().CompileAndRegisterAll(expressions));
		}

		[Test]
		public void NamesInStringLiteralsDoNotCreateDependencies()
		{
			var expressions = new List<ExpressionInfo>
			{
				Float("alpha", "return \"beta()\".Length;"),
				Float("beta", "return \"alpha()\".Length;")
			};

			Assert.DoesNotThrow(() => NewRegistry().CompileAndRegisterAll(expressions));
		}

		[Test]
		public void MemberCallIsNotMistakenForExpressionCall()
		{
			// An expression callable as `Count` must not be treated as a dependency of a body that merely
			// invokes the member `xs.Count()` — the preceding `.` rules it out even when the name matches
			// exactly (alias forces the callable name to `Count`, so this isolates the dot, not casing).
			var expressions = new List<ExpressionInfo>
			{
				new("counter",
					Array.Empty<(string type, string name)>(),
					"float",
					Array.Empty<string>(),
					Array.Empty<string>(),
					"return 0f;",
					"Count"),
				new("user",
					new (string type, string name)[] { ("float list", "xs") },
					"float",
					Array.Empty<string>(),
					Array.Empty<string>(),
					"return xs.Count();")
			};

			Assert.DoesNotThrow(() => NewRegistry().CompileAndRegisterAll(expressions));
		}

		[Test]
		public void GenuineCallStillResolvesOutOfOrder()
		{
			// `beta` calls `alpha` but is declared first; the scan must still order `alpha` ahead of it.
			var expressions = new List<ExpressionInfo>
			{
				Float("beta", "return alpha() + 1f;"),
				Float("alpha", "return 2f;")
			};

			var results = NewRegistry().CompileAndRegisterAllBestEffort(expressions);

			Assert.That(results.All(r => r.Success), Is.True,
				"Expected both expressions to compile; errors: " +
				string.Join("; ", results.Where(r => !r.Success).Select(r => $"{r.Info.Id}: {r.Error}")));
		}
	}
}
