#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assembler.Style
{
	/// <summary>
	/// Immutable projection of <see cref="StyleGuide"/> the consumers read. The
	/// renderer enforces the typed fields directly; the voxelization prompts only need
	/// the prose <see cref="ToVoxelProse"/> renders.
	/// </summary>
	public sealed record StyleBible
	{
		public PaletteInfo Palette { get; init; } = new();
		public VoxelFormInfo VoxelForm { get; init; } = new();
		public LightingInfo Lighting { get; init; } = new();
		public CameraInfo Camera { get; init; } = new();
		public PostSettings Post { get; init; } = new();
		public UiSettings Ui { get; init; } = new();
		public string AdditionalNotes { get; init; } = string.Empty;

		/// <summary>
		/// Renders the voxel-relevant fields into the prose blob the voxelization
		/// prompts consume (replacing the hand-typed style-guidance string), then
		/// appends the free-text notes. This is the ADVISORY half — the engine enforces
		/// palette/form directly; the prompts only need persuading.
		/// </summary>
		public string ToVoxelProse()
		{
			var sb = new StringBuilder();
			sb.Append("Scale: 1 voxel = ").Append(VoxelForm.WorldUnitsPerVoxel)
				.Append(" world units; size every asset to this one ratio.\n");
			sb.Append("Use at most ").Append(Palette.MaxColoursPerAsset)
				.Append(" colours per asset, drawn from the shared master palette.\n");
			sb.Append("Form: ").Append(VoxelForm.Edges switch
			{
				EdgeTreatment.Outline => "clean blocky shapes that read well with an outline pass",
				EdgeTreatment.Bevel => "slightly bevelled cubes",
				_ => "raw cubic voxels",
			}).Append(VoxelForm.Faces == FaceStyle.FlatMatte ? ", flat matte materials.\n" : ", softly specular materials.\n");
			if (AdditionalNotes.Length > 0)
			{
				sb.Append('\n').Append(AdditionalNotes).Append('\n');
			}

			return sb.ToString();
		}
	}

	public sealed record PaletteInfo
	{
		public IReadOnlyList<Color> Swatches { get; init; } = Array.Empty<Color>();
		public int MaxColoursPerAsset { get; init; } = 12;
		public bool QuantizeToMaster { get; init; } = true;
		public Vector2 SaturationRange { get; init; } = new(0.25f, 0.85f);
		public Vector2 ValueRange { get; init; } = new(0.15f, 0.95f);

		/// <summary>
		/// Nearest master swatch to an arbitrary colour — the enforced quantiser. Returns
		/// the input unchanged when no master palette is defined. Uses squared RGB distance;
		/// swap for a perceptual (Lab/Oklab) metric if the linear nearest looks wrong.
		/// </summary>
		public Color Quantize(Color colour)
		{
			if (Swatches.Count == 0)
			{
				return colour;
			}

			var best = Swatches[0];
			var bestDistance = float.MaxValue;
			foreach (var swatch in Swatches)
			{
				var dr = swatch.r - colour.r;
				var dg = swatch.g - colour.g;
				var db = swatch.b - colour.b;
				var distance = (dr * dr) + (dg * dg) + (db * db);
				if (distance < bestDistance)
				{
					bestDistance = distance;
					best = swatch;
				}
			}

			return best;
		}
	}

	public sealed record VoxelFormInfo
	{
		public float WorldUnitsPerVoxel { get; init; } = 0.1f;
		public EdgeTreatment Edges { get; init; } = EdgeTreatment.Outline;
		public float OutlineThicknessPx { get; init; } = 1.5f;
		public FaceStyle Faces { get; init; } = FaceStyle.FlatMatte;
		public int DefaultTolerance { get; init; } = 1;
	}

	public sealed record LightingInfo
	{
		public Vector3 KeyEulerAngles { get; init; }
		public Color KeyColour { get; init; } = Color.white;
		public float KeyIntensity { get; init; } = 1f;
		public Color AmbientSky { get; init; }
		public Color AmbientEquator { get; init; }
		public Color AmbientGround { get; init; }
		public float ShadowStrength { get; init; } = 0.6f;
		public float ShadowSoftness { get; init; } = 1f;
	}

	public sealed record CameraInfo
	{
		public IReadOnlyList<CameraPreset> Presets { get; init; } = Array.Empty<CameraPreset>();
	}
}
