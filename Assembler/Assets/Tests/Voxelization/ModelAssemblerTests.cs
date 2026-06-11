using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Voxelization;
using Assembler.Voxels;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	internal sealed class StubScriptRunner : IPartScriptRunner
	{
		private readonly Func<string, VoxelModel> _build;

		public StubScriptRunner(Func<string, VoxelModel> build) => _build = build;

		public static StubScriptRunner Failing(string message) =>
			new(_ => throw new InvalidOperationException(message));

		public Task<VoxelModel> RunAsync(string source, CancellationToken ct) =>
			Task.FromResult(_build(source));
	}

	public sealed class ModelAssemblerTests
	{
		private static AssembledModel Assemble(VoxelRigModel model, IPartScriptRunner? runner = null) =>
			new ModelAssembler(runner ?? StubScriptRunner.Failing("no scripts expected"))
				.AssembleAsync(model, CancellationToken.None).GetAwaiter().GetResult();

		[Test]
		public void Villager_AssemblesCleanAndOnScale()
		{
			var assembled = Assemble(VillagerFixture.Build());

			Assert.That(assembled.AssemblyIssues.IsValid, Is.True,
				string.Join("\n", assembled.AssemblyIssues.Issues));
			Assert.That(assembled.Composed.Voxels.Count, Is.EqualTo(52));
			Assert.That(assembled.Composed.Size.y, Is.EqualTo(10), "villager must be exactly 10 voxels tall");
		}

		[Test]
		public void WorldPivots_AccumulateDownTheChain()
		{
			var assembled = Assemble(VillagerFixture.Build());

			// head pivot [0,4,0] under torso pivot [0,4,0] => world [0,8,0].
			Assert.That(assembled.FindPart("head")!.WorldPivot, Is.EqualTo(new Vector3Int(0, 8, 0)));
			// mirrored arm pivot [2,3,0] under torso => world [2,7,0].
			Assert.That(assembled.FindPart("arm.R")!.WorldPivot, Is.EqualTo(new Vector3Int(2, 7, 0)));
		}

		[Test]
		public void Mirror_ReflectsGeometryThroughLocalOrigin()
		{
			var source = LayersCodec.ToModel(new Dictionary<Vector3Int, byte>
			{
				[new Vector3Int(1, 0, 0)] = 1,
				[new Vector3Int(2, 1, 0)] = 1,
			}, new[] { new PaletteEntry('A', new Color32(255, 0, 0, 255)) });

			var mirrored = ModelAssembler.MirrorGrid(source, MirrorAxis.X, new VoxelRigModel
			{
				Palette = new[] { new PaletteEntry('A', new Color32(255, 0, 0, 255)) },
			});

			Assert.That(mirrored.Voxels.Keys, Is.EquivalentTo(new[]
			{
				new Vector3Int(-1, 0, 0),
				new Vector3Int(-2, 1, 0),
			}));
		}

		[Test]
		public void ScriptColoursOutsidePalette_ReportPaletteBreach()
		{
			var model = new VoxelRigModel
			{
				Id = "t",
				Unit = 1f,
				RealWorldHeight = 1f,
				Palette = new[] { new PaletteEntry('A', new Color32(255, 0, 0, 255)) },
				Parts = new[]
				{
					new VoxelPart
					{
						Id = "p",
						Pivot = Vector3Int.zero,
						Data = new ScriptPartData(Vector3Int.one, Vector3Int.zero, "return b.Build();"),
					},
				},
			};

			var rogue = new VoxelModel(
				new Dictionary<Vector3Int, byte> { [Vector3Int.zero] = 1 },
				new[] { new Color32(0, 255, 0, 255) },
				Vector3Int.zero,
				Vector3Int.zero);

			var assembled = Assemble(model, new StubScriptRunner(_ => rogue));

			Assert.That(assembled.AssemblyIssues.Issues.Single().Code, Is.EqualTo(IssueCode.PaletteBreach));
		}

		[Test]
		public void PlannedPart_ReportsNotAuthored()
		{
			var model = new VoxelRigModel
			{
				Id = "t",
				Unit = 1f,
				RealWorldHeight = 1f,
				Palette = new[] { new PaletteEntry('A', new Color32(255, 0, 0, 255)) },
				Parts = new[]
				{
					new VoxelPart { Id = "p", Pivot = Vector3Int.zero },
				},
			};

			var assembled = Assemble(model);

			Assert.That(assembled.AssemblyIssues.Issues.Single().Code, Is.EqualTo(IssueCode.NotAuthored));
		}
	}
}
