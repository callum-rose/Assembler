using System.Collections.Generic;
using Assembler.Compiler.Compiler;
using Assembler.Libraries;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Compiler
{
	public class PositionListCompileTests
	{
		[Test]
		public void BuildsAVectorListWithNewAddAndToList()
		{
			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterType(typeof(PositionList));

			var body = @"
var b = new PositionList();
for (int x = 0; x < 3; x++) {
    b.Add(new UnityEngine.Vector3(x, 0f, 0f));
}
return b.ToList();";

			var func = compiler.CompileFunc<List<Vector3>>(body);
			var result = func();

			CollectionAssert.AreEqual(
				new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 0) },
				result);
		}
	}
}
