using Assembler.Compiler.Compiler;
using NUnit.Framework;

namespace Tests.Compiler
{
	/// <summary>
	/// Shared base for the compiler test suites: a fresh <see cref="ExpressionMethodCompiler"/> per test
	/// (via <c>[SetUp]</c>), replacing the <c>var compiler = new ExpressionMethodCompiler();</c> line that
	/// otherwise opened all 160 tests.
	/// </summary>
	public abstract class CompilerTestBase
	{
		protected ExpressionMethodCompiler compiler = null!;

		[SetUp]
		public void SetUpCompiler() => compiler = new ExpressionMethodCompiler();
	}
}
