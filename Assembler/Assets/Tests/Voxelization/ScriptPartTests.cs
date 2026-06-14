using System.Threading;
using Assembler.Voxelization;
using Assembler.Voxels.Scripting;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	/// <summary>
	/// M2: a hand-written oak-tree part script runs through the real
	/// <see cref="VoxelScriptExecutor"/> (expression compiler) with the Y-up
	/// convention and assembles/validates sanely.
	/// </summary>
	public sealed class ScriptPartTests
	{
		private const string OakScript =
			"var trunk = b.Hex(\"#6b4a2b\");\n" +
			"var leaf = b.Hex(\"#2f7d32\");\n" +
			"for (int y = 0; y < 30; y++)\n" +
			"{\n" +
			"    b.Set(0, y, 0, trunk);\n" +
			"    if (y > 4 && y % 7 == 0)\n" +
			"    {\n" +
			"        b.Set(1, y, 0, trunk);\n" +
			"        b.Set(-1, y, 0, trunk);\n" +
			"    }\n" +
			"}\n" +
			"b.Ellipsoid(0, 38, 0, 9, 11, 9, leaf);\n" +
			"b.Ellipsoid(0, 34, 0, 11, 8, 11, leaf);\n" +
			"return b.Build();";

		[Test]
		public void OakTreeScript_RunsAssemblesAndValidates()
		{
			var model = new VoxelRigModel
			{
				Id = "oak_tree",
				TargetHeight = 50,
				Palette = new[]
				{
					new PaletteEntry('T', new Color32(0x6b, 0x4a, 0x2b, 0xff)),
					new PaletteEntry('L', new Color32(0x2f, 0x7d, 0x32, 0xff)),
				},
				Parts = new[]
				{
					new VoxelPart
					{
						Id = "tree",
						Pivot = Vector3Int.zero,
						Data = new ScriptPartData(new Vector3Int(23, 50, 23), new Vector3Int(-11, 0, -11), OakScript),
					},
				},
			};

			var runner = new ExecutorPartScriptRunner(new VoxelScriptExecutor());
			var assembled = new ModelAssembler(runner)
				.AssembleAsync(model, CancellationToken.None).GetAwaiter().GetResult();

			Assert.That(assembled.AssemblyIssues.IsValid, Is.True,
				string.Join("\n", assembled.AssemblyIssues.Issues));
			Assert.That(assembled.Composed.Voxels.Count, Is.GreaterThan(100));

			var report = new ModelValidator().Validate(assembled, ReferenceBrief.None);
			Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues));
		}

		[Test]
		public void RunScriptAsync_ReportsCompileErrorsAsScriptIssues()
		{
			var model = new VoxelRigModel
			{
				Id = "broken",
				TargetHeight = 1,
				Palette = new[] { new PaletteEntry('A', new Color32(255, 0, 0, 255)) },
				Parts = new[]
				{
					new VoxelPart
					{
						Id = "p",
						Pivot = Vector3Int.zero,
						Data = new ScriptPartData(Vector3Int.one, Vector3Int.zero, "this is not C# at all"),
					},
				},
			};

			var runner = new ExecutorPartScriptRunner(new VoxelScriptExecutor());
			var assembled = new ModelAssembler(runner)
				.AssembleAsync(model, CancellationToken.None).GetAwaiter().GetResult();

			Assert.That(assembled.AssemblyIssues.Issues, Has.Some.Matches<ValidationIssue>(
				i => i.Code == IssueCode.ScriptFailed && i.PartId == "p"));
		}
	}
}
