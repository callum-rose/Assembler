using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;
using Assembler.Voxels;
using Assembler.Voxels.Generation;
using Assembler.Voxels.Pipeline;
using Assembler.Voxels.Pipeline.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxels
{
	public class VoxelVisionTests
	{
		// ---- Phase 0: Anthropic vision wire shape --------------------------

		[Test]
		public void AnthropicImage_Base64_RoundTrips()
		{
			var bytes = new byte[] { 1, 2, 3, 4, 250 };
			var image = new AnthropicImage(bytes);
			Assert.AreEqual(System.Convert.ToBase64String(bytes), image.Base64());
			Assert.AreEqual("image/png", image.MediaType);
		}

		[Test]
		public void AnthropicImage_ToWireDictionary_HasImageBlockShape()
		{
			var bytes = new byte[] { 9, 8, 7 };
			var image = new AnthropicImage(bytes, "image/jpeg");

			var wire = image.ToWireDictionary();

			Assert.AreEqual("image", wire["type"].GetString());
			var source = wire["source"];
			Assert.AreEqual(JsonValueKind.Object, source.ValueKind);
			Assert.AreEqual("base64", source.GetProperty("type").GetString());
			Assert.AreEqual("image/jpeg", source.GetProperty("media_type").GetString());
			Assert.AreEqual(System.Convert.ToBase64String(bytes), source.GetProperty("data").GetString());
		}

		[Test]
		public void ReferenceImageStyle_Wrap_IncludesSubjectAndArtDirection()
		{
			var styled = ReferenceImageStyle.Wrap("a knight");
			StringAssert.Contains("a knight", styled);
			StringAssert.Contains("voxel art", styled);
			StringAssert.Contains("silhouette", styled);
		}

		// ---- Phase 1: reference-image stage --------------------------------

		[Test]
		public void GenerateReferenceImage_WithFakeProvider_StoresImages()
		{
			var fake = new FakeImageGenerator(new[] { new byte[] { 1 }, new byte[] { 2 } });
			var ctx = new VoxelPipelineContext { ImageGenerator = fake, UserPrompt = "a tree", ReferenceVariations = 2 };

			var result = new GenerateReferenceImageStage().ExecuteAsync(ctx, CancellationToken.None).Result;

			Assert.IsNotNull(result.ReferenceImages);
			Assert.AreEqual(2, result.ReferenceImages!.Count);
			Assert.AreEqual("a tree", fake.LastPromptContains);
		}

		[Test]
		public void GenerateReferenceImage_NullProvider_IsNoOp()
		{
			var ctx = new VoxelPipelineContext { ImageGenerator = NullImageGenerator.Instance, UserPrompt = "a tree" };

			var result = new GenerateReferenceImageStage().ExecuteAsync(ctx, CancellationToken.None).Result;

			Assert.IsNull(result.ReferenceImages);
		}

		// ---- Phase 2: palette quantiser ------------------------------------

		[Test]
		public void PaletteQuantizer_ReducesToRequestedColorCount()
		{
			var pixels = new List<Color32>();
			for (var i = 0; i < 100; i++)
			{
				pixels.Add(new Color32(250, 10, 10, 255));   // red cluster
			}

			for (var i = 0; i < 100; i++)
			{
				pixels.Add(new Color32(10, 10, 250, 255));   // blue cluster
			}

			var palette = PaletteQuantizer.Quantize(pixels, maxColors: 2);

			Assert.AreEqual(2, palette.Length);
			// Dominant clusters come first; both reds and blues should be represented.
			var hasRed = false;
			var hasBlue = false;
			foreach (var c in palette)
			{
				hasRed |= c.r > c.b;
				hasBlue |= c.b > c.r;
			}

			Assert.IsTrue(hasRed && hasBlue, "Both colour clusters should survive quantisation.");
		}

		[Test]
		public void PaletteQuantizer_IsDeterministic()
		{
			var pixels = new List<Color32>
			{
				new(200, 30, 30, 255), new(30, 200, 30, 255), new(30, 30, 200, 255),
				new(200, 200, 30, 255), new(30, 200, 200, 255),
			};

			var a = PaletteQuantizer.ToHexList(PaletteQuantizer.Quantize(pixels, 3));
			var b = PaletteQuantizer.ToHexList(PaletteQuantizer.Quantize(pixels, 3));
			Assert.AreEqual(a, b);
		}

		[Test]
		public void PaletteQuantizer_IgnoresTransparentPixels()
		{
			var pixels = new List<Color32>
			{
				new(255, 0, 0, 0),     // transparent — should be ignored
				new(0, 255, 0, 255),   // opaque green
			};

			var palette = PaletteQuantizer.Quantize(pixels, 4);
			Assert.AreEqual(1, palette.Length);
			Assert.Greater(palette[0].g, palette[0].r);
		}

		// ---- Phase 3: vision critique no-op safety -------------------------

		[Test]
		public void VisionCritiqueRefine_NoRenders_IsNoOp()
		{
			var ctx = new VoxelPipelineContext
			{
				SystemPrompt = "sys",
				GoxelTextZUp = "0 0 0 ff0000",
				// AnthropicClient deliberately null — the no-op must return before using it.
			};

			var result = new VisionCritiqueRefineStage().ExecuteAsync(ctx, CancellationToken.None).Result;

			Assert.AreEqual("0 0 0 ff0000", result.GoxelTextZUp);
		}

		// ---- Phase 4: geometry validation ----------------------------------

		[Test]
		public void ValidateGeometry_DetectsAndPrunesFloatingVoxel()
		{
			// A 2x1x1 connected bar plus one detached voxel far away.
			var voxels = new Dictionary<Vector3Int, byte>
			{
				[new Vector3Int(0, 0, 0)] = 1,
				[new Vector3Int(1, 0, 0)] = 1,
				[new Vector3Int(10, 10, 10)] = 1, // floating orphan
			};
			var model = new VoxelModel(voxels, new[] { new Color32(255, 0, 0, 255) },
				new Vector3Int(0, 0, 0), new Vector3Int(10, 10, 10));
			var ctx = new VoxelPipelineContext { Model = model, GoxelTextZUp = GoxelTextWriter.Write(model) };

			var result = new ValidateGeometryStage(pruneOrphans: true, orphanMaxSize: 4)
				.ExecuteAsync(ctx, CancellationToken.None).Result;

			Assert.IsNotNull(result.Geometry);
			Assert.AreEqual(1, result.Geometry!.PrunedVoxels);
			Assert.AreEqual(1, result.Geometry.ComponentCount, "After pruning only the main body remains.");
			Assert.AreEqual(2, result.Model!.Voxels.Count);
			Assert.IsFalse(result.Model.Voxels.ContainsKey(new Vector3Int(10, 10, 10)));
		}

		[Test]
		public void ValidateGeometry_ReportsComponentsWhenPruningDisabled()
		{
			var voxels = new Dictionary<Vector3Int, byte>
			{
				[new Vector3Int(0, 0, 0)] = 1,
				[new Vector3Int(1, 0, 0)] = 1,
				[new Vector3Int(10, 10, 10)] = 1,
			};
			var model = new VoxelModel(voxels, new[] { new Color32(255, 0, 0, 255) },
				new Vector3Int(0, 0, 0), new Vector3Int(10, 10, 10));
			var ctx = new VoxelPipelineContext { Model = model };

			var result = new ValidateGeometryStage(pruneOrphans: false)
				.ExecuteAsync(ctx, CancellationToken.None).Result;

			Assert.AreEqual(2, result.Geometry!.ComponentCount);
			Assert.AreEqual(0, result.Geometry.PrunedVoxels);
			Assert.AreEqual(3, result.Model!.Voxels.Count, "Nothing pruned — model unchanged.");
		}

		[Test]
		public void ValidateGeometry_SymmetricModelScoresHigh()
		{
			// Symmetric about x: voxels at x=0 and x=2 mirror across plane x=1.
			var voxels = new Dictionary<Vector3Int, byte>
			{
				[new Vector3Int(0, 0, 0)] = 1,
				[new Vector3Int(2, 0, 0)] = 1,
				[new Vector3Int(1, 1, 0)] = 1, // on the mirror plane
			};
			var model = new VoxelModel(voxels, new[] { new Color32(255, 0, 0, 255) },
				new Vector3Int(0, 0, 0), new Vector3Int(2, 1, 0));
			var ctx = new VoxelPipelineContext { Model = model };

			var result = new ValidateGeometryStage(pruneOrphans: false)
				.ExecuteAsync(ctx, CancellationToken.None).Result;

			Assert.AreEqual(1f, result.Geometry!.SymmetryScore, 0.0001f);
		}

		[Test]
		public void ValidateGeometry_LopsidedModelScoresLow()
		{
			// All voxels on one side of the bbox centre — poor symmetry.
			var voxels = new Dictionary<Vector3Int, byte>
			{
				[new Vector3Int(0, 0, 0)] = 1,
				[new Vector3Int(1, 0, 0)] = 1,
				[new Vector3Int(0, 1, 0)] = 1,
				[new Vector3Int(10, 0, 0)] = 1, // lone voxel pushing the bbox right
			};
			var model = new VoxelModel(voxels, new[] { new Color32(255, 0, 0, 255) },
				new Vector3Int(0, 0, 0), new Vector3Int(10, 1, 0));
			var ctx = new VoxelPipelineContext { Model = model };

			var result = new ValidateGeometryStage(pruneOrphans: false)
				.ExecuteAsync(ctx, CancellationToken.None).Result;

			// Only the two bbox-extreme voxels mirror; the inner pair don't → 0.5,
			// well below a symmetric model's 1.0.
			Assert.Less(result.Geometry!.SymmetryScore, 0.75f);
		}

		// ---- Fakes ---------------------------------------------------------

		private sealed class FakeImageGenerator : IImageGenerator
		{
			private readonly IReadOnlyList<byte[]> _images;
			public string LastPromptContains { get; private set; } = string.Empty;

			public FakeImageGenerator(IReadOnlyList<byte[]> images) => _images = images;

			public Task<IReadOnlyList<byte[]>> GenerateAsync(string prompt, int variations, CancellationToken ct)
			{
				// Record the subject so the test can confirm the prompt threaded through.
				LastPromptContains = prompt.Contains("a tree") ? "a tree" : prompt;
				return Task.FromResult(_images);
			}
		}
	}
}
