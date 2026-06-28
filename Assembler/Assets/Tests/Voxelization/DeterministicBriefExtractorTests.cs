using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Assembler.Anthropic;
using Assembler.Voxelization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	/// <summary>
	/// Covers the deterministic pixel analysis (<see cref="ReferenceImageAnalysis"/>)
	/// and the extractor that drives it (<see cref="DeterministicBriefExtractor"/>):
	/// silhouette mask correctness, fully-enclosed gap preservation, per-cell area
	/// coverage thresholding, palette extraction, and that a symmetric input yields
	/// symmetric rows with no symmetrise repair step.
	/// </summary>
	public sealed class DeterministicBriefExtractorTests
	{
		private static readonly Color32 Transparent = new(0, 0, 0, 0);
		private static readonly Color32 Grey = new(128, 128, 128, 255);
		private static readonly Color32 Red = new(200, 40, 40, 255);
		private static readonly Color32 Blue = new(40, 60, 210, 255);

		// ---- ReferenceImageAnalysis: silhouette --------------------------------

		[Test]
		public void Silhouette_PreservesFullyEnclosedGap()
		{
			// The donut hole is interior transparency — a per-pixel alpha test reads
			// it as empty without any border flood-fill.
			var pixels = Grid(new[]
			{
				"####",
				"#..#",
				"####",
			});

			var spec = ReferenceImageAnalysis.Silhouette("front", pixels, rows: 3, cellCoverage: 0.5f, bgTolerance: 0.12f);

			Assert.That(spec.Rows, Is.EqualTo(new[] { "####", "#__#", "####" }));
			Assert.That(spec.Size, Is.EqualTo(new Vector3Int(4, 3, 0)));
		}

		[Test]
		public void Silhouette_SymmetricInput_ProducesSymmetricRows_WithoutRepair()
		{
			var pixels = Grid(new[]
			{
				".##.",
				"####",
				"#..#",
			});

			var spec = ReferenceImageAnalysis.Silhouette("front", pixels, rows: 3, cellCoverage: 0.5f, bgTolerance: 0.12f);

			foreach (var row in spec.Rows)
			{
				Assert.That(row, Is.EqualTo(new string(row.Reverse().ToArray())), $"row '{row}' is not left-right symmetric");
			}
		}

		[Test]
		public void Silhouette_TrimsBackgroundMargin_GridHugsTheBoundingBox()
		{
			// A subject inset in transparent margin: the grid hugs the bounding box,
			// so no empty border rows/cols survive (the trim hack is unnecessary).
			var pixels = Grid(new[]
			{
				"......",
				".####.",
				".####.",
				"......",
			});

			var spec = ReferenceImageAnalysis.Silhouette("front", pixels, rows: 2, cellCoverage: 0.5f, bgTolerance: 0.12f);

			Assert.That(spec.Rows, Is.EqualTo(new[] { "####", "####" }));
		}

		[TestCase(0.5f, "#_")]
		[TestCase(0.4f, "##")]
		public void Silhouette_UsesPerCellAreaCoverage_NotSingleSample(float coverage, string expected)
		{
			// Bounding box is 3 wide x 2 tall; one output row of 2 cells. The left
			// cell is fully covered (1.0), the right cell is half covered (0.5), so
			// the threshold — strictly greater-than — decides the right cell.
			var pixels = Grid(new[]
			{
				"###",
				"##.",
			});

			var spec = ReferenceImageAnalysis.Silhouette("front", pixels, rows: 1, cellCoverage: coverage, bgTolerance: 0.12f);

			Assert.That(spec.Rows.Single(), Is.EqualTo(expected));
		}

		[Test]
		public void Silhouette_OpaquePlainBackground_KeyedByCornerColourTolerance()
		{
			// The subject is inset from the corners so all four corners read the
			// plain grey background; the grid then keys foreground by colour distance.
			var legend = new Dictionary<char, Color32> { ['.'] = Grey, ['#'] = Red };
			var pixels = Grid(new[]
			{
				"....",
				".##.",
				"####",
				"....",
			}, legend);

			var spec = ReferenceImageAnalysis.Silhouette("front", pixels, rows: 2, cellCoverage: 0.5f, bgTolerance: 0.12f);

			Assert.That(spec.Rows, Is.EqualTo(new[] { "_##_", "####" }));
		}

		[Test]
		public void Silhouette_EmptyImage_ReturnsEmptySpecForTheFace()
		{
			var pixels = Grid(new[] { "..", ".." });

			var spec = ReferenceImageAnalysis.Silhouette("top", pixels, rows: 4, cellCoverage: 0.5f, bgTolerance: 0.12f);

			Assert.That(spec.Face, Is.EqualTo("top"));
			Assert.That(spec.IsEmpty, Is.True);
		}

		// ---- ReferenceImageAnalysis: palette -----------------------------------

		[Test]
		public void Palette_ExtractsFlatForegroundColours_ExcludingBackground()
		{
			var legend = new Dictionary<char, Color32> { ['.'] = Transparent, ['R'] = Red, ['B'] = Blue };
			var pixels = Grid(new[]
			{
				".RRRRBB.",
				".RRRRBB.",
				".RRRRBB.",
				".RRRRBB.",
			}, legend);

			var palette = ReferenceImageAnalysis.Palette(new[] { pixels }, maxColours: 12, bgTolerance: 0.12f, mergeDistance: 0.06f);

			var hex = palette.Select(e => e.ToHex()).ToList();
			Assert.That(hex, Has.Count.EqualTo(2));
			Assert.That(hex, Does.Contain(new PaletteEntry('x', Red).ToHex()));
			Assert.That(hex, Does.Contain(new PaletteEntry('x', Blue).ToHex()));
		}

		[Test]
		public void Palette_ExcludesAntiAliasedEdgePixels_KeepingOnlyInteriorColours()
		{
			// A one-pixel edge ring (E) around a flat core (R), on transparent
			// margin. Edge pixels touch the background so they are never interior;
			// only the core colour survives into the palette.
			var edge = new Color32(120, 120, 120, 255);
			var legend = new Dictionary<char, Color32> { ['.'] = Transparent, ['E'] = edge, ['R'] = Red };
			var pixels = Grid(new[]
			{
				".......",
				".EEEEE.",
				".ERRRE.",
				".ERRRE.",
				".ERRRE.",
				".EEEEE.",
				".......",
			}, legend);

			var palette = ReferenceImageAnalysis.Palette(new[] { pixels }, maxColours: 12, bgTolerance: 0.12f, mergeDistance: 0.06f);

			var hex = palette.Select(e => e.ToHex()).ToList();
			Assert.That(hex, Is.EqualTo(new[] { new PaletteEntry('x', Red).ToHex() }));
		}

		[Test]
		public void Palette_CapsAtMaxColours()
		{
			var legend = new Dictionary<char, Color32>
			{
				['.'] = Transparent,
				['a'] = new(10, 0, 0, 255),
				['b'] = new(60, 0, 0, 255),
				['c'] = new(110, 0, 0, 255),
				['d'] = new(160, 0, 0, 255),
			};
			var pixels = Grid(new[]
			{
				".aabbccdd.",
				".aabbccdd.",
				".aabbccdd.",
			}, legend);

			var palette = ReferenceImageAnalysis.Palette(new[] { pixels }, maxColours: 2, bgTolerance: 0.12f, mergeDistance: 0.06f);

			Assert.That(palette, Has.Count.EqualTo(2));
		}

		[Test]
		public void Palette_AssignsDistinctSingleCharacterKeys()
		{
			var legend = new Dictionary<char, Color32> { ['.'] = Transparent, ['R'] = Red, ['B'] = Blue };
			var pixels = Grid(new[]
			{
				".RRBB.",
				".RRBB.",
				".RRBB.",
			}, legend);

			var palette = ReferenceImageAnalysis.Palette(new[] { pixels }, maxColours: 12, bgTolerance: 0.12f, mergeDistance: 0.06f);

			Assert.That(palette.Select(e => e.Key).Distinct().Count(), Is.EqualTo(palette.Count));
			Assert.That(palette.All(e => e.Key != PaletteEntry.EmptyKey && e.Key != PaletteEntry.EmptyCell));
		}

		// ---- DeterministicBriefExtractor: end to end through a real PNG --------

		[Test]
		public void Extract_TransparentPng_ProducesExactSilhouetteAndPalette()
		{
			var asset = new ManifestAsset
			{
				Id = "crate",
				Height = 3,
				References = new[] { new ReferenceImage("front.png", "front") },
			};
			var image = Png(new[]
			{
				"####",
				"#..#",
				"####",
			});

			var brief = Extract(asset, Extractor(out _), (asset.References[0], image));

			Assert.That(brief.Source, Is.EqualTo("crate"));
			Assert.That(brief.PrimarySilhouette.Rows, Is.EqualTo(new[] { "####", "#__#", "####" }));
			Assert.That(brief.Palette.Select(e => e.ToHex()).Single(), Is.EqualTo(new PaletteEntry('x', Red).ToHex()));
		}

		[Test]
		public void Extract_OpaquePlainBackgroundPng_KeyedByTolerance()
		{
			var asset = new ManifestAsset
			{
				Id = "crate",
				Height = 2,
				References = new[] { new ReferenceImage("front.png", "front") },
			};
			var image = Png(new[]
			{
				"....",
				".##.",
				"####",
				"....",
			}, new Dictionary<char, Color32> { ['.'] = Grey, ['#'] = Blue });

			var brief = Extract(asset, Extractor(out _), (asset.References[0], image));

			Assert.That(brief.PrimarySilhouette.Rows, Is.EqualTo(new[] { "_##_", "####" }));
			Assert.That(brief.Palette.Select(e => e.ToHex()).Single(), Is.EqualTo(new PaletteEntry('x', Blue).ToHex()));
		}

		[Test]
		public void Extract_CoAxialFaces_ProduceOneSilhouettePerAxis()
		{
			var asset = new ManifestAsset
			{
				Id = "crate",
				Height = 2,
				References = new[]
				{
					new ReferenceImage("front.png", "front"),
					new ReferenceImage("back.png", "back"),
				},
			};
			var brief = Extract(asset, Extractor(out _),
				(asset.References[0], BlockPng()), (asset.References[1], BlockPng()));

			Assert.That(brief.Silhouettes, Has.Count.EqualTo(1));
			Assert.That(brief.Silhouettes.Single().Face, Is.EqualTo("front"));
		}

		[Test]
		public void Extract_DoesNotCallTheGateway_WhenSemanticFieldsDisabled()
		{
			var asset = new ManifestAsset
			{
				Id = "crate",
				Height = 2,
				References = new[] { new ReferenceImage("front.png", "front") },
			};

			var brief = Extract(asset, Extractor(out var gateway), (asset.References[0], BlockPng()));

			Assert.That(gateway.Calls, Is.Empty, "the deterministic path must not make a vision call by default");
			Assert.That(brief.Proportions, Is.Empty);
			Assert.That(brief.SignatureFeatures, Is.Empty);
		}

		[Test]
		public void Extract_FillsSemanticFieldsFromOneSlimVisionCall_WhenEnabled()
		{
			var asset = new ManifestAsset
			{
				Id = "crate",
				Height = 2,
				References = new[] { new ReferenceImage("front.png", "front") },
			};
			var config = VoxelizationConfig.Default with { ExtractSemanticBriefFields = true };
			var gateway = new FakeGateway().Enqueue(@"```brief
reference_brief:
  proportions: { body: 0.6, lid: 0.4 }
  signature_features: [""wooden slats""]
```");
			var extractor = new DeterministicBriefExtractor(gateway, config);

			var brief = Extract(asset, extractor, (asset.References[0], BlockPng()));

			Assert.That(gateway.Calls, Has.Count.EqualTo(1));
			Assert.That(gateway.Calls.Single().Stage, Is.EqualTo(BriefExtractor.Stage));
			Assert.That(brief.Proportions["body"], Is.EqualTo(0.6f).Within(1e-4));
			Assert.That(brief.SignatureFeatures, Does.Contain("wooden slats"));

			// The silhouette is still measured from pixels, not read from the model.
			Assert.That(brief.PrimarySilhouette.Rows, Is.EqualTo(new[] { "##", "##" }));
		}

		[Test]
		public void Extract_KeepsMeasuredFields_WhenTheSemanticCallReturnsGarbage()
		{
			var asset = new ManifestAsset
			{
				Id = "crate",
				Height = 2,
				References = new[] { new ReferenceImage("front.png", "front") },
			};
			var config = VoxelizationConfig.Default with { ExtractSemanticBriefFields = true };
			var gateway = new FakeGateway().Enqueue("no fenced block here");
			var extractor = new DeterministicBriefExtractor(gateway, config);

			var brief = Extract(asset, extractor, (asset.References[0], BlockPng()));

			// Advisory fields are best-effort: a bad reply leaves them empty rather
			// than failing the asset or looping on a retry.
			Assert.That(gateway.Calls, Has.Count.EqualTo(1));
			Assert.That(brief.Proportions, Is.Empty);
			Assert.That(brief.PrimarySilhouette.Rows, Is.EqualTo(new[] { "##", "##" }));
		}

		// ---- helpers -----------------------------------------------------------

		private static DeterministicBriefExtractor Extractor(out FakeGateway gateway)
		{
			gateway = new FakeGateway();
			return new DeterministicBriefExtractor(gateway, VoxelizationConfig.Default);
		}

		private static ReferenceBrief Extract(
			ManifestAsset asset,
			DeterministicBriefExtractor extractor,
			params (ReferenceImage Label, AnthropicImage Image)[] images) =>
			extractor.ExtractAsync(new SetManifest { Assets = new[] { asset } }, asset, images, CancellationToken.None)
				.GetAwaiter().GetResult();

		private static readonly Dictionary<char, Color32> DefaultLegend = new() { ['.'] = Transparent, ['#'] = Red };

		/// <summary>A 2x2 red block inset in a transparent margin — silhouette ["##", "##"] at Height 2.</summary>
		private static AnthropicImage BlockPng() => Png(new[]
		{
			"....",
			".##.",
			".##.",
			"....",
		});

		/// <summary>Builds pixels from a top-row-first grid, flipped into GetPixels32 (row 0 = bottom) order.</summary>
		private static ReferenceImageAnalysis.Pixels Grid(string[] rows, IDictionary<char, Color32>? legend = null)
		{
			legend ??= DefaultLegend;
			var height = rows.Length;
			var width = rows[0].Length;
			var data = new Color32[width * height];
			for (var r = 0; r < height; r++)
			{
				for (var c = 0; c < width; c++)
				{
					var y = height - 1 - r;
					data[y * width + c] = legend[rows[r][c]];
				}
			}

			return new ReferenceImageAnalysis.Pixels(data, width, height);
		}

		/// <summary>Encodes a top-row-first grid to PNG bytes wrapped as an <see cref="AnthropicImage"/>.</summary>
		private static AnthropicImage Png(string[] rows, IDictionary<char, Color32>? legend = null)
		{
			var pixels = Grid(rows, legend);
			var texture = new Texture2D(pixels.Width, pixels.Height, TextureFormat.RGBA32, mipChain: false);
			texture.SetPixels32(pixels.Data);
			texture.Apply();
			var bytes = ImageConversion.EncodeToPNG(texture);
			UnityEngine.Object.DestroyImmediate(texture);
			return new AnthropicImage("image/png", bytes);
		}
	}
}
