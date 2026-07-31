using System.Collections.Generic;
using Assembler.Compiler.Compiler;
using Assembler.Libraries;
using UnityEngine;

namespace Spike.CompilerHarness.Cases
{
	/// <summary>
	/// Port of <c>Assets/Tests/Compiler/PositionListCompileTests.cs</c> (1 case). Small but load-bearing:
	/// <c>PositionList</c> is how real descriptors build a <c>vector list</c>, so this is the shape the
	/// descriptor half depends on working.
	/// </summary>
	public static class PositionListCases
	{
		private const string Suite = "PositionList";

		public static void Register(SpikeCaseList list)
		{
			list.Add($"{Suite}/BuildsAVectorListWithNewAddAndToList", () =>
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

				Check.Sequence(
					result,
					new List<Vector3> { new(0, 0, 0), new(1, 0, 0), new(2, 0, 0) });
			});
		}
	}
}
