using System;
using System.Linq;
using Assembler.Voxelization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	/// <summary>
	/// Pure-application tests for the refine op vocabulary, built on the villager
	/// fixture: each op rewrites exactly what it names, untouched parts stay
	/// reference-equal (so the export is bit-identical), and referential mistakes
	/// throw with feedback rather than corrupting the model.
	/// </summary>
	public sealed class ModelEditsTests
	{
		private static VoxelRigModel Apply(VoxelRigModel model, params ModelEditOp[] ops) =>
			ModelEdits.Apply(model, ReferenceBrief.None, ops).Model;

		[Test]
		public void Parse_ReadsTheOpVocabulary()
		{
			var ops = ModelEdits.Parse(@"
- { op: recolour, key: B, colour: ""#cc2222"" }
- { op: move_pivot, part: head, delta: [0, 1, 0] }
- { op: reauthor, part: arm.L, instructions: ""longer"", size: [1, 5, 1] }
- { op: replan, reason: ""needs a hat"" }");

			Assert.That(ops, Has.Count.EqualTo(4));
			Assert.That(ops[0], Is.EqualTo(new RecolourOp('B', new Color32(0xcc, 0x22, 0x22, 0xff))));
			Assert.That(ops[1], Is.EqualTo(new MovePivotOp("head", new Vector3Int(0, 1, 0))));
			Assert.That(ops[2], Is.EqualTo(new ReauthorOp("arm.L", "longer", new Vector3Int(1, 5, 1), null)));
			Assert.That(ops[3], Is.EqualTo(new ReplanOp("needs a hat")));
		}

		[Test]
		public void Parse_RejectsAMalformedBlock()
		{
			Assert.That(() => ModelEdits.Parse("op: recolour"), Throws.TypeOf<FormatException>());
			Assert.That(() => ModelEdits.Parse("- { op: frobnicate }"),
				Throws.TypeOf<FormatException>().With.Message.Contains("frobnicate"));
			Assert.That(() => ModelEdits.Parse("- { op: recolour, colour: \"#fff000\" }"),
				Throws.TypeOf<FormatException>().With.Message.Contains("key"));
		}

		[Test]
		public void Recolour_UpdatesBothTheModelAndBriefPalettes()
		{
			var model = VillagerFixture.Build();
			var brief = new ReferenceBrief
			{
				Source = "ref.png",
				Palette = new[] { new PaletteEntry('B', new Color32(0x3a, 0x5f, 0xcd, 0xff)) },
			};

			var red = new Color32(0xcc, 0x22, 0x22, 0xff);
			var (edited, editedBrief, _) = ModelEdits.Apply(model, brief, new ModelEditOp[] { new RecolourOp('B', red) });

			Assert.That(edited.Palette.Single(e => e.Key == 'B').Colour, Is.EqualTo(red));
			Assert.That(editedBrief.Palette.Single(e => e.Key == 'B').Colour, Is.EqualTo(red));

			// A palette-only edit leaves the whole part list untouched (reference-equal).
			Assert.That(ReferenceEquals(edited.Parts, model.Parts), Is.True);
		}

		[Test]
		public void AddColour_AppendsAndEnforcesTheLimit()
		{
			var model = VillagerFixture.Build();
			var edited = Apply(model, new AddColourOp('R', new Color32(0xcc, 0x22, 0x22, 0xff)));
			Assert.That(edited.Palette.Select(e => e.Key), Is.EqualTo(new[] { 'S', 'B', 'K', 'R' }));

			Assert.That(() => Apply(model, new AddColourOp('S', Color.white)),
				Throws.TypeOf<FormatException>().With.Message.Contains("already exists"));
		}

		[Test]
		public void Remap_RewritesOnlyTheNamedPartsLayers()
		{
			var model = VillagerFixture.Build();
			var originalTorso = model.FindPart("torso");

			var edited = Apply(model, new RemapPartColourOp("arm.L", 'B', 'S'));

			var arm = (LayersPartData)edited.FindPart("arm.L")!.Data;
			Assert.That(arm.Layers, Is.EqualTo(new[] { "S", "S", "S", "S" }));

			// Every other part is reference-equal — its YAML cannot have changed.
			Assert.That(ReferenceEquals(edited.FindPart("torso"), originalTorso), Is.True);
			Assert.That(ReferenceEquals(edited.FindPart("head"), model.FindPart("head")), Is.True);
			Assert.That(ReferenceEquals(edited.FindPart("leg.L"), model.FindPart("leg.L")), Is.True);
		}

		[Test]
		public void MovePivot_ReReflectsTheMirrorTwin()
		{
			var model = VillagerFixture.Build();

			var edited = Apply(model, new MovePivotOp("arm.L", new Vector3Int(0, 1, 0)));

			Assert.That(edited.FindPart("arm.L")!.Pivot, Is.EqualTo(new Vector3Int(-2, 4, 0)));
			// arm.R mirrors arm.L on x, so its pivot follows: (2, 3, 0) -> (2, 4, 0).
			Assert.That(edited.FindPart("arm.R")!.Pivot, Is.EqualTo(new Vector3Int(2, 4, 0)));
		}

		[Test]
		public void MovePivot_RejectsMovingACentrePartOffThePlane()
		{
			var model = VillagerFixture.Build();
			Assert.That(() => Apply(model, new MovePivotOp("torso", new Vector3Int(1, 0, 0))),
				Throws.TypeOf<FormatException>().With.Message.Contains("mirror plane"));

			// Moving the same centre part in y/z is fine.
			var edited = Apply(model, new MovePivotOp("torso", new Vector3Int(0, -1, 0)));
			Assert.That(edited.FindPart("torso")!.Pivot, Is.EqualTo(new Vector3Int(0, 3, 0)));
		}

		[Test]
		public void MoveOffset_ShiftsTheGeometryWindow()
		{
			var model = VillagerFixture.Build();
			var edited = Apply(model, new MoveOffsetOp("torso", new Vector3Int(0, 0, -1)));
			var torso = (LayersPartData)edited.FindPart("torso")!.Data;
			Assert.That(torso.Offset, Is.EqualTo(new Vector3Int(-1, 0, -2)));

			Assert.That(() => Apply(model, new MoveOffsetOp("arm.R", Vector3Int.up)),
				Throws.TypeOf<FormatException>().With.Message.Contains("mirror"));
		}

		[Test]
		public void Delete_CascadesToChildrenMirrorsAndPoses()
		{
			var model = VillagerFixture.Build();

			var edited = Apply(model, new DeletePartOp("torso"));

			// torso -> head, arm.L (children), arm.R (child + mirror of arm.L) all go.
			Assert.That(edited.Parts.Select(p => p.Id), Is.EqualTo(new[] { "leg.L", "leg.R" }));
			// The wave pose only rotated arm.R, which is gone — its entry is dropped.
			Assert.That(edited.Poses.Single(p => p.Name == "wave").Rotations, Is.Empty);
			Assert.That(edited.Poses.Select(p => p.Name), Is.EqualTo(new[] { "idle", "wave" }));
		}

		[Test]
		public void Reauthor_IsReturnedNotApplied()
		{
			var model = VillagerFixture.Build();
			var originalHead = model.FindPart("head");

			var (edited, _, reauthors) = ModelEdits.Apply(
				model, ReferenceBrief.None, new ModelEditOp[] { new ReauthorOp("head", "rounder", null, null) });

			Assert.That(reauthors.Single().PartId, Is.EqualTo("head"));
			// The model is untouched — the orchestrator runs the author afterwards.
			Assert.That(ReferenceEquals(edited.FindPart("head"), originalHead), Is.True);
		}

		[Test]
		public void ReferentialErrors_ThrowWithUsefulMessages()
		{
			var model = VillagerFixture.Build();

			Assert.That(() => Apply(model, new RecolourOp('Z', Color.white)),
				Throws.TypeOf<FormatException>().With.Message.Contains("Z"));
			Assert.That(() => Apply(model, new RemapPartColourOp("ghost", 'B', 'S')),
				Throws.TypeOf<FormatException>().With.Message.Contains("ghost"));
			Assert.That(() => Apply(model, new RemapPartColourOp("arm.L", 'B', 'Q')),
				Throws.TypeOf<FormatException>().With.Message.Contains("Q"));
			Assert.That(() => Apply(model, new RemapPartColourOp("arm.R", 'B', 'S')),
				Throws.TypeOf<FormatException>().With.Message.Contains("mirror"));
			Assert.That(() => Apply(model, new DeletePartOp("nope")),
				Throws.TypeOf<FormatException>().With.Message.Contains("nope"));
			Assert.That(() => Apply(model, new ReauthorOp("leg.R", "x", null, null)),
				Throws.TypeOf<FormatException>().With.Message.Contains("source"));
		}

		[Test]
		public void Apply_LeavesTheWrittenYamlIdenticalOutsideTheEditedPart()
		{
			var model = VillagerFixture.Build();
			var before = VModelYaml.Write(model);

			var edited = Apply(model, new RemapPartColourOp("arm.L", 'B', 'S'));
			var after = VModelYaml.Write(edited);

			// Only the arm.L block differs — split on the part markers and compare
			// every other block verbatim.
			var beforeBlocks = SplitParts(before);
			var afterBlocks = SplitParts(after);
			Assert.That(afterBlocks.Keys, Is.EqualTo(beforeBlocks.Keys));
			foreach (var id in beforeBlocks.Keys.Where(k => k != "arm.L"))
			{
				Assert.That(afterBlocks[id], Is.EqualTo(beforeBlocks[id]), $"part '{id}' block changed");
			}

			Assert.That(afterBlocks["arm.L"], Is.Not.EqualTo(beforeBlocks["arm.L"]));
		}

		private static System.Collections.Generic.Dictionary<string, string> SplitParts(string yaml)
		{
			var blocks = new System.Collections.Generic.Dictionary<string, string>();
			var current = string.Empty;
			var sb = new System.Text.StringBuilder();
			foreach (var line in yaml.Split('\n'))
			{
				if (line.TrimStart().StartsWith("- id:", StringComparison.Ordinal))
				{
					if (current.Length > 0)
					{
						blocks[current] = sb.ToString();
					}

					current = line.Substring(line.IndexOf("- id:", StringComparison.Ordinal) + 5).Trim();
					sb = new System.Text.StringBuilder();
				}

				if (current.Length > 0)
				{
					sb.Append(line).Append('\n');
				}
			}

			if (current.Length > 0)
			{
				blocks[current] = sb.ToString();
			}

			return blocks;
		}
	}
}
