using Assembler.Compiler.Compiler;
using NUnit.Framework;
using UnityEngine;

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
	}
}