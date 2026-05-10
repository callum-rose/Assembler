using System;
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
			try
			{
				var compiler = new ExpressionMethodCompiler();
				var expression = "new UnityEngine.Vector3(0, UnityEngine.Random.Range(-2f, 2f), 0);";
				
				var compiled = compiler.Compile(expression, typeof(Vector3), out _);
				
				Debug.Log($"[DEBUG_LOG] Compiled: {compiled}");
				var result = compiled.DynamicInvoke();
				Debug.Log($"[DEBUG_LOG] Result: {result}");
				
				Assert.IsNotNull(compiled);
				Assert.IsInstanceOf<Vector3>(result);
			}
			catch (Exception e)
			{
				Debug.LogError($"[DEBUG_LOG] Exception: {e}");
				throw;
			}
		}
	}
}