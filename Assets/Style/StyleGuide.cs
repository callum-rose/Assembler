#nullable enable
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assembler.Style
{
	/// <summary>
	/// The project-wide art bible: the single source of truth for every cross-game
	/// consistency lever. Operator-edited as one asset (mirrors
	/// <c>VoxelizationSettings</c>), projected to an immutable <see cref="StyleBible"/>
	/// for the two consumers — the runtime renderer (the ENFORCED layer: palette
	/// quantisation, post volume, lighting rig, camera presets, UI kit applied to
	/// every game regardless of what was generated) and the voxelization prompts (the
	/// ADVISORY layer: <see cref="StyleBible.ToVoxelProse"/> replaces the hand-typed
	/// style-guidance string).
	/// </summary>
	[CreateAssetMenu(menuName = "Assembler/Style Guide", fileName = "StyleGuide")]
	public sealed class StyleGuide : ScriptableObject
	{
		[Header("Palette")]
		public PaletteSettings Palette = new();

		[Header("Voxel form")]
		public VoxelFormSettings VoxelForm = new();

		[Header("Lighting")]
		public LightingSettings Lighting = new();

		[Header("Camera")]
		public CameraSettings Camera = new();

		[Header("Post-processing")]
		public PostSettings Post = new();

		[Header("UI")]
		public UiSettings Ui = new();

		[Header("Free-text guidance (appended to the voxel prompts verbatim)")]
		[TextArea(3, 8)]
		public string AdditionalNotes = string.Empty;

		/// <summary>Projects the editable asset onto the immutable bible consumers read.</summary>
		public StyleBible ToBible() => new()
		{
			Palette = Palette.ToInfo(),
			VoxelForm = VoxelForm.ToInfo(),
			Lighting = Lighting.ToInfo(),
			Camera = Camera.ToInfo(),
			Post = Post,
			Ui = Ui,
			AdditionalNotes = AdditionalNotes.Trim(),
		};
	}

	// ── Palette ──────────────────────────────────────────────────────────────────
	[Serializable]
	public sealed class PaletteSettings
	{
		[Tooltip("The master palette every generated voxel colour quantises to — the single " +
			"strongest cohesion lever. Keep it curated (~32-64 swatches).")]
		public Color[] Swatches = Array.Empty<Color>();

		[Tooltip("Hard ceiling on colours any one asset may use (the planning prompt already caps at 12).")]
		[Range(2, 16)]
		public int MaxColoursPerAsset = 12;

		[Tooltip("ENFORCED: snap every authored/generated colour to the nearest master swatch at build time.")]
		public bool QuantizeToMaster = true;

		[Tooltip("Saturation window all swatches / derived colours must sit within (mood discipline).")]
		public Vector2 SaturationRange = new(0.25f, 0.85f);

		[Tooltip("Value/brightness window — keeps one game from going muddy and another neon.")]
		public Vector2 ValueRange = new(0.15f, 0.95f);

		public PaletteInfo ToInfo() => new()
		{
			Swatches = Swatches,
			MaxColoursPerAsset = MaxColoursPerAsset,
			QuantizeToMaster = QuantizeToMaster,
			SaturationRange = SaturationRange,
			ValueRange = ValueRange,
		};
	}

	// ── Voxel form ───────────────────────────────────────────────────────────────
	public enum EdgeTreatment
	{
		None,
		Outline,
		Bevel,
	}

	public enum FaceStyle
	{
		FlatMatte,
		SoftSpecular,
	}

	[Serializable]
	public sealed class VoxelFormSettings
	{
		[Tooltip("THE shared atomic unit: the world size of one voxel cell, constant across every " +
			"game so a Pong paddle and a Tetris block are built from identical cubes. This is the " +
			"scale authority the manifest stage already defers to.")]
		public float WorldUnitsPerVoxel = 0.1f;

		[Tooltip("The one stylistic signature fork: outlined / bevelled / raw cubes.")]
		public EdgeTreatment Edges = EdgeTreatment.Outline;

		[Tooltip("Outline width in screen pixels (ignored unless Edges = Outline).")]
		public float OutlineThicknessPx = 1.5f;

		public FaceStyle Faces = FaceStyle.FlatMatte;

		[Tooltip("Default ± voxels tolerance carried into every manifest asset.")]
		[Min(0)]
		public int DefaultTolerance = 1;

		public VoxelFormInfo ToInfo() => new()
		{
			WorldUnitsPerVoxel = WorldUnitsPerVoxel,
			Edges = Edges,
			OutlineThicknessPx = OutlineThicknessPx,
			Faces = Faces,
			DefaultTolerance = DefaultTolerance,
		};
	}

	// ── Lighting ─────────────────────────────────────────────────────────────────
	[Serializable]
	public sealed class LightingSettings
	{
		[Header("Key light")]
		public Vector3 KeyEulerAngles = new(50f, -30f, 0f);
		public Color KeyColour = Color.white;

		[Min(0)]
		public float KeyIntensity = 1f;

		[Header("Fill / ambient (defaults mirror the current Bootstrap scene)")]
		public Color AmbientSky = new(0.212f, 0.227f, 0.259f);
		public Color AmbientEquator = new(0.114f, 0.125f, 0.133f);
		public Color AmbientGround = new(0.047f, 0.043f, 0.035f);

		[Range(0f, 1f)]
		public float ShadowStrength = 0.6f;

		[Range(0f, 2f)]
		public float ShadowSoftness = 1f;

		public LightingInfo ToInfo() => new()
		{
			KeyEulerAngles = KeyEulerAngles,
			KeyColour = KeyColour,
			KeyIntensity = KeyIntensity,
			AmbientSky = AmbientSky,
			AmbientEquator = AmbientEquator,
			AmbientGround = AmbientGround,
			ShadowStrength = ShadowStrength,
			ShadowSoftness = ShadowSoftness,
		};
	}

	// ── Camera ───────────────────────────────────────────────────────────────────
	public enum CameraProjection
	{
		Perspective,
		Orthographic,
	}

	[Serializable]
	public sealed class CameraPreset
	{
		public string Name = "default";
		public CameraProjection Projection = CameraProjection.Perspective;

		[Tooltip("Perspective field of view — share one value across presets so every game reads as the same world.")]
		[Range(20f, 90f)]
		public float FieldOfView = 50f;

		public float NearClip = 0.1f;
		public float FarClip = 200f;
	}

	[Serializable]
	public sealed class CameraSettings
	{
		[Tooltip("Named presets the generator PICKS from rather than inventing a framing per game.")]
		public List<CameraPreset> Presets = new();

		public CameraInfo ToInfo() => new() { Presets = Presets.AsReadOnly() };
	}

	// ── Post-processing & UI (asset references — already Unity-serialisable, no projection) ──
	[Serializable]
	public sealed class PostSettings
	{
		[Tooltip("ENFORCED global volume profile applied to every game — the strongest single mood unifier.")]
		public VolumeProfile? GlobalProfile;

		[Tooltip("Global colour-grading LUT (lives inside the profile; surfaced here for clarity).")]
		public Texture? GradingLut;

		public bool AmbientOcclusion = true;
		public bool Bloom = true;
		public bool Vignette = true;

		// Deliberately absent / forced off downstream: motion blur, depth-of-field and
		// chromatic aberration — they vary unpredictably and hurt gameplay clarity.
	}

	[Serializable]
	public sealed class UiSettings
	{
		[Tooltip("The component kit the AI composes from (the existing UiPrefabLibrary). Typed loosely to " +
			"keep this schema assembly free of a UI dependency; the consumer casts it.")]
		public ScriptableObject? PrefabLibrary;

		public TMP_FontAsset? Font;

		[Min(0)]
		public float CornerRadiusPx = 8f;

		[Min(0)]
		public float SpacingUnitPx = 8f;

		[Tooltip("Accent index into Palette.Swatches — keeps the UI accent inside the master palette.")]
		[Min(0)]
		public int AccentSwatchIndex;
	}
}
