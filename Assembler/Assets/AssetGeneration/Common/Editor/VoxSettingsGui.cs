#nullable enable

using System;
using System.Collections.Generic;
using Assembler.AssetGeneration.MeshToVoxel;
using Assembler.AssetGeneration.MeshToVoxels;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.EditorCommon
{
	/// <summary>
	/// The mesh → voxel control set (Resolution / Shape / Taubin / Colour) shared verbatim by the
	/// standalone Mesh → Voxel window and stage 3 of the Text → Voxels pipeline window. Holds one
	/// serializable <see cref="Draft"/> of the ~34 knobs plus the (window-owned) master palette,
	/// draws them with <see cref="Draw"/>, assembles the engine-free <see cref="Settings"/> with
	/// <see cref="ToSettings"/>, and persists everything under a single pref key via
	/// <see cref="Save"/>/<see cref="Load"/> — replacing the two hand-mirrored 34-key Get/Set walls.
	/// </summary>
	public sealed class VoxSettingsGui
	{
		private const int FineNodeWarningDim = 120;

		private readonly Draft _draft = new();
		private VoxMasterPalette? _masterPalette;
		private bool _showAdvancedWeights;

		/// <summary>The persisted knob values. Public fields so Unity's <see cref="JsonUtility"/> serializes them.</summary>
		[Serializable]
		public sealed class Draft
		{
			public ResolutionInput ResolutionInput = ResolutionInput.MaxDimSlider;
			public int MaxDimVoxels = 24;
			public float VoxelWorldSize = 0.1f;
			public float TargetWorldSize = 2f;

			public bool GridSearch = true;
			public bool ScaleFlex = true;
			public bool ThinFeatureKeep = true;
			public int FineFactor = 3;
			public float Coverage = 0.5f;
			public bool RemoveFloaters = true;
			public int CleanupStrength = 1;
			public bool FillCorners;
			public float CornerFillColourTolerance = 0.1f;
			public int CornerFillNeighbourThreshold = CornerFill.DefaultNeighbourThreshold;
			public bool CornerFillRequireMajority = true;
			public SymmetryAxes Symmetry = SymmetryAxes.None;
			public bool ForceMirror;

			public float FaceWeight = 1f;
			public float IouWeight = 1f;
			public float GapWeight = 2f;
			public float ColWeight;

			public bool UvDilate = true;
			public int UvDilatePasses = UvIslandDilation.DefaultPasses;
			public bool MultiSampleColour = true;
			public float PottsStrength = 0.5f;
			public ColourMode ColourMode = ColourMode.PerModelPalette;
			public int PaletteSize = 8;
			public float ConsolidateTolerance = 0.06f;
			public int ConsolidateMaxColours;
			public bool NormalConsistency;

			public int TaubinPasses = 5;
			public float TaubinLambda = 0.5f;
			public float TaubinMu = 0.53f;
			public bool SurfaceReproject;
		}

		/// <summary>Draw the Resolution / Shape / Taubin / Colour sections with their bold headers.</summary>
		public void Draw()
		{
			EditorGUILayout.LabelField("Resolution", EditorStyles.boldLabel);
			DrawResolution();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);
			DrawShape();
			DrawTaubin();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Colour", EditorStyles.boldLabel);
			DrawColour();
		}

		/// <summary>Assemble the engine-free voxeliser settings for one pipeline run.</summary>
		public Settings ToSettings() => new()
		{
			ResolutionInput = _draft.ResolutionInput,
			MaxDimVoxels = _draft.MaxDimVoxels,
			VoxelWorldSize = _draft.VoxelWorldSize,
			TargetWorldSize = _draft.TargetWorldSize,
			GridSearch = _draft.GridSearch,
			ScaleFlex = _draft.ScaleFlex,
			ThinFeatureKeep = _draft.ThinFeatureKeep,
			FineFactor = _draft.FineFactor,
			Coverage = _draft.Coverage,
			RemoveFloaters = _draft.RemoveFloaters,
			CleanupStrength = _draft.CleanupStrength,
			FillCorners = _draft.FillCorners,
			CornerFillColourTolerance = _draft.CornerFillColourTolerance,
			CornerFillNeighbourThreshold = _draft.CornerFillNeighbourThreshold,
			CornerFillRequireMajority = _draft.CornerFillRequireMajority,
			Symmetry = _draft.Symmetry,
			ForceMirror = _draft.ForceMirror,
			FaceWeight = _draft.FaceWeight,
			IouWeight = _draft.IouWeight,
			GapWeight = _draft.GapWeight,
			ColWeight = _draft.ColWeight,
			UvDilate = _draft.UvDilate,
			UvDilatePasses = _draft.UvDilatePasses,
			MultiSampleColour = _draft.MultiSampleColour,
			PottsStrength = _draft.PottsStrength,
			TaubinPasses = _draft.TaubinPasses,
			TaubinLambda = _draft.TaubinLambda,
			TaubinMu = _draft.TaubinMu,
			SurfaceReproject = _draft.SurfaceReproject,
			ColourMode = _draft.ColourMode,
			PaletteSize = _draft.PaletteSize,
			ConsolidateTolerance = _draft.ConsolidateTolerance,
			ConsolidateMaxColours = _draft.ConsolidateMaxColours,
			MasterPalette = _draft.ColourMode == ColourMode.MasterPalette
				? ToCorePalette(_masterPalette != null ? _masterPalette.ToColor32() : DefaultMasterPalette.Colors)
				: null,
			NormalConsistency = _draft.NormalConsistency,
		};

		/// <summary>
		/// Overwrite the knobs from an AI-generated config's voxel settings (Import from AI). The
		/// master palette is the window's, never the AI's, so it is left untouched.
		/// </summary>
		public void ApplyImported(Settings v)
		{
			_draft.ResolutionInput = v.ResolutionInput;
			_draft.MaxDimVoxels = v.MaxDimVoxels;
			_draft.VoxelWorldSize = v.VoxelWorldSize;
			_draft.TargetWorldSize = v.TargetWorldSize;
			_draft.GridSearch = v.GridSearch;
			_draft.ScaleFlex = v.ScaleFlex;
			_draft.ThinFeatureKeep = v.ThinFeatureKeep;
			_draft.FineFactor = v.FineFactor;
			_draft.Coverage = v.Coverage;
			_draft.RemoveFloaters = v.RemoveFloaters;
			_draft.CleanupStrength = v.CleanupStrength;
			_draft.FillCorners = v.FillCorners;
			_draft.CornerFillColourTolerance = v.CornerFillColourTolerance;
			_draft.CornerFillNeighbourThreshold = v.CornerFillNeighbourThreshold;
			_draft.CornerFillRequireMajority = v.CornerFillRequireMajority;
			_draft.Symmetry = v.Symmetry;
			_draft.ForceMirror = v.ForceMirror;
			_draft.FaceWeight = v.FaceWeight;
			_draft.IouWeight = v.IouWeight;
			_draft.GapWeight = v.GapWeight;
			_draft.ColWeight = v.ColWeight;
			_draft.UvDilate = v.UvDilate;
			_draft.UvDilatePasses = v.UvDilatePasses;
			_draft.MultiSampleColour = v.MultiSampleColour;
			_draft.PottsStrength = v.PottsStrength;
			_draft.ColourMode = v.ColourMode;
			_draft.PaletteSize = v.PaletteSize;
			_draft.ConsolidateTolerance = v.ConsolidateTolerance;
			_draft.ConsolidateMaxColours = v.ConsolidateMaxColours;
			_draft.NormalConsistency = v.NormalConsistency;
			_draft.TaubinPasses = v.TaubinPasses;
			_draft.TaubinLambda = v.TaubinLambda;
			_draft.TaubinMu = v.TaubinMu;
			_draft.SurfaceReproject = v.SurfaceReproject;
		}

		/// <summary>Persist the knobs (one JSON blob) and the master-palette reference (as a GUID) under one pref key.</summary>
		public void Save(string prefKey)
		{
			EditorPrefs.SetString(prefKey, JsonUtility.ToJson(_draft));

			var assetPath = _masterPalette != null ? AssetDatabase.GetAssetPath(_masterPalette) : "";
			EditorPrefs.SetString(prefKey + ".PaletteGuid",
				string.IsNullOrEmpty(assetPath) ? "" : AssetDatabase.AssetPathToGUID(assetPath));
		}

		public void Load(string prefKey)
		{
			var json = EditorPrefs.GetString(prefKey, "");
			if (!string.IsNullOrEmpty(json))
			{
				JsonUtility.FromJsonOverwrite(json, _draft);
			}

			var guid = EditorPrefs.GetString(prefKey + ".PaletteGuid", "");
			if (!string.IsNullOrEmpty(guid))
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				_masterPalette = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<VoxMasterPalette>(path);
			}
		}

		// The master-palette swatches come from Unity-side types (VoxMasterPalette / DefaultMasterPalette);
		// the engine-free Settings takes the core Rgba32, so convert at this boundary.
		private static Rgba32[] ToCorePalette(IReadOnlyList<Color32> colours)
		{
			var result = new Rgba32[colours.Count];
			for (int i = 0; i < colours.Count; i++)
			{
				Color32 c = colours[i];
				result[i] = new Rgba32(c.r, c.g, c.b, c.a);
			}

			return result;
		}

		private void DrawResolution()
		{
			_draft.ResolutionInput = (ResolutionInput)EditorGUILayout.EnumPopup(
				new GUIContent("Input mode",
					"Max dim slider: set the voxel budget along the longest axis directly. World size: derive it "
					+ "from the model's intended in-game size ÷ the shared global voxel size, so every asset shares "
					+ "one voxel scale (the mode to use for a cohesive set)."),
				_draft.ResolutionInput);

			using (new EditorGUI.IndentLevelScope())
			{
				if (_draft.ResolutionInput == ResolutionInput.WorldSize)
				{
					_draft.VoxelWorldSize = EditorGUILayout.FloatField(
						new GUIContent("Voxel world size",
							"Edge length of one voxel in world units, shared across every asset. Smaller = finer/"
							+ "more voxels. This is the global scale the whole set is quantised to."),
						_draft.VoxelWorldSize);
					_draft.TargetWorldSize = EditorGUILayout.FloatField(
						new GUIContent("Target world size",
							"How big this model's longest axis should be in-game, world units. Divided by the voxel "
							+ "world size to pick the voxel budget — so a bigger prop gets more voxels."),
						_draft.TargetWorldSize);
					EditorGUILayout.LabelField(
						new GUIContent(" ", "The resulting voxel budget for the longest axis, after rounding and clamping to the supported 4–96 range."),
						new GUIContent($"→ {ToSettings().ResolveMaxDimVoxels()} voxels (longest axis, clamped 4–96)"));
				}
				else
				{
					_draft.MaxDimVoxels = EditorGUILayout.IntSlider(
						new GUIContent("Max dimension (voxels)",
							"Voxels along the longest bounding-box axis; the other axes scale to match. Keep it low "
							+ "(~10–16 for characters) for the chunky stylised read; the pipeline is designed to "
							+ "behave across the whole 4–96 range."),
						_draft.MaxDimVoxels, 4, 96);
				}
			}

			Settings settings = ToSettings();
			int fineDim = settings.ResolveMaxDimVoxels() * settings.ResolveFineFactor();
			if (fineDim > FineNodeWarningDim)
			{
				EditorGUILayout.HelpBox(
					$"Fine grid is ~{fineDim}³ nodes — the fast-winding-number occupancy pass will take tens of "
					+ "seconds. Lower the resolution or the fine factor.",
					MessageType.Warning);
			}
		}

		private void DrawShape()
		{
			_draft.GridSearch = EditorGUILayout.ToggleLeft(
				new GUIContent("Grid placement search",
					"Score candidate grid phases/scales against the fine grid (face economy, IoU, air-gap preservation) and voxelise on the winner. Off = today's fixed placement."),
				_draft.GridSearch);
			using (new EditorGUI.IndentLevelScope())
			{
				using (new EditorGUI.DisabledScope(!_draft.GridSearch))
				{
					_draft.ScaleFlex = EditorGUILayout.ToggleLeft(
						new GUIContent("Scale flex",
							"Let the search also stretch the voxel grid per-axis to snap the model's extent onto a "
							+ "whole voxel count (a 7.5-voxel-long bar becomes exactly 7 or 8), clamped to ±10%. "
							+ "Removes the ragged half-voxel at the end of a run. Needs the grid search on."),
						_draft.ScaleFlex);
				}
			}

			_draft.ThinFeatureKeep = EditorGUILayout.ToggleLeft(
				new GUIContent("Thin-feature keep",
					"Force-keep sub-voxel silhouette features (legs, ears, antennae, a mug handle) that a plain "
					+ "coverage vote would erase — but only where they connect to the model's main body, so "
					+ "disconnected specks still die. Builds the fine-grid analysis (see Fine factor)."),
				_draft.ThinFeatureKeep);

			using (new EditorGUI.DisabledScope(!_draft.GridSearch && !_draft.ThinFeatureKeep))
			{
				using (new EditorGUI.IndentLevelScope())
				{
					_draft.FineFactor = EditorGUILayout.IntSlider(
						new GUIContent("Fine factor",
							"The grid search and thin-keep first voxelise at this multiple of the target resolution "
							+ "to analyse features, then vote down. Higher = finer analysis but the fine grid grows "
							+ "as factor³ — the main cost driver (watch the fine-grid-size warning above). Only used "
							+ "when the search or thin-keep is on."),
						_draft.FineFactor, 2, 4);
				}
			}

			_draft.Coverage = EditorGUILayout.Slider(
				new GUIContent("Coverage threshold",
					"Fraction of a coarse voxel's fine cells that must be solid for it to fill (unless thin-keep "
					+ "forces it). Higher trims jagged one-voxel slivers off diagonal surfaces for a boxier read; "
					+ "lower keeps more bulk."),
				_draft.Coverage, 0f, 1f);

			_draft.RemoveFloaters = EditorGUILayout.ToggleLeft(
				new GUIContent("Remove floaters",
					"Drop disconnected voxel islands whose fine support never touches the model's main connected "
					+ "component — the stray specks left by messy geometry. The largest island is always kept, so "
					+ "the model can never vanish."),
				_draft.RemoveFloaters);

			_draft.CleanupStrength = EditorGUILayout.IntSlider(
				new GUIContent("Cleanup strength",
					"Rank morphological close→open passes: close fills lone pits/notches, open shaves lone bumps/"
					+ "spikes — flatter faces, cleaner silhouette. Corners and edges are left intact (unlike a "
					+ "classic close→open). Never shaves kept thin features, never welds real air gaps, and "
					+ "re-bridges anything it splits. 1 = one pass, 2 = stronger, 0 = off."),
				_draft.CleanupStrength, 0, 2);

			_draft.FillCorners = EditorGUILayout.ToggleLeft(
				new GUIContent("Fill corners",
					"Fill concave corners/notches so the silhouette boxes out. An empty voxel fills when three "
					+ "same-colour face-neighbours meet it at a shared vertex (a genuine concave corner — fill that "
					+ "colour) OR it is walled in by a deep-enough pocket of occupied neighbours (fill the modal "
					+ "colour). The shared-vertex gate avoids inappropriate fills — a same-colour straddle across a "
					+ "thin sheet spans only two axes and is left alone. Real air gaps (leg gaps, handle holes) are "
					+ "protected from the corner fill, but a deep pocket (walled in on most sides) fills anyway — it "
					+ "can't be a see-through gap. Repeats until nothing new qualifies."),
				_draft.FillCorners);
			if (_draft.FillCorners)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					_draft.CornerFillColourTolerance = EditorGUILayout.Slider(
						new GUIContent("Colour tolerance",
							"How close two neighbour colours must be (Oklab distance) to count as the same for the "
							+ "same-colour corner rule. 0 = exact match (too strict — near-identical shades read "
							+ "as different); raise it so similar shades group and clean corners fill. Too high and "
							+ "distinct regions merge, blurring boundaries. ~0.1 is a good start."),
						_draft.CornerFillColourTolerance, 0f, 0.5f);
					_draft.CornerFillNeighbourThreshold = EditorGUILayout.IntSlider(
						new GUIContent("Pocket threshold",
							"How many of the 6 face-neighbours must be occupied to fill a cell regardless of colour "
							+ "(the deep-pocket rule). Higher = more conservative, fewer pockets filled: 6 fills only "
							+ "fully-enclosed holes, 4 also boxes out shallow dents. 5 is a good start."),
						_draft.CornerFillNeighbourThreshold, 4, 6);
					_draft.CornerFillRequireMajority = EditorGUILayout.ToggleLeft(
						new GUIContent("Require colour majority",
							"Only fill a deep pocket when its modal (most common) neighbour colour is a strict "
							+ "majority. Stops a pocket where two colour regions meet from being smeared with an "
							+ "arbitrary side. Off = always fill a deep pocket with whichever colour is modal."),
						_draft.CornerFillRequireMajority);
				}
			}

			_draft.Symmetry = (SymmetryAxes)EditorGUILayout.EnumFlagsField(
				new GUIContent("Force symmetry",
					"Make the model symmetric across the centre of its occupied bounds on each ticked axis. X/Y/Z "
					+ "are the grid axes (Y is up); pick whichever gives the intended left-right symmetry. Applied "
					+ "last, so the silhouette is guaranteed symmetric. Default (Force mirror off) is a union — "
					+ "keep a voxel if it or its mirror is filled, preserving both halves' features and asymmetric "
					+ "colour where geometry already exists on both sides."),
				_draft.Symmetry);
			if (_draft.Symmetry != SymmetryAxes.None)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					_draft.ForceMirror = EditorGUILayout.ToggleLeft(
						new GUIContent("Force mirror (exact)",
							"Reflect the dominant half (the one with more voxels) onto the other, OVERRIDING what "
							+ "was there — an exact mirror in both geometry and colour, discarding the input's "
							+ "asymmetry. Use this when you want a guaranteed-clean symmetric result; leave off for "
							+ "the union that keeps features and real colour from both sides."),
						_draft.ForceMirror);
				}
			}

			_showAdvancedWeights = EditorGUILayout.Foldout(
				_showAdvancedWeights,
				new GUIContent("Advanced: search score weights",
					"Relative weights of the terms the grid-placement search maximises. Defaults 1 / 1 / 2 / 0 are "
					+ "tuned so merging air gaps (the fatal failure) costs most. Leave alone unless the geometry "
					+ "terms aren't separating candidates."),
				toggleOnLabelClick: true);
			if (_showAdvancedWeights)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					_draft.FaceWeight = EditorGUILayout.Slider(
						new GUIContent("Face economy (S_face)",
							"Rewards placements with fewer exposed faces per voxel (equivalent-cube faces ÷ actual "
							+ "faces) — favours chunky, axis-aligned blocks over stair-stepped diagonals. Default 1."),
						_draft.FaceWeight, 0f, 4f);
					_draft.IouWeight = EditorGUILayout.Slider(
						new GUIContent("Shape IoU (S_iou)",
							"Rewards overlap between the coarse voxels and the fine occupancy — keeps the blocky "
							+ "model faithful to the source silhouette. Default 1."),
						_draft.IouWeight, 0f, 4f);
					_draft.GapWeight = EditorGUILayout.Slider(
						new GUIContent("Air-gap keep (S_gap)",
							"Penalises covering air-gap cells (the space between a dog's four legs, a mug's handle "
							+ "hole). Weighted 2× by default because merging a gap is the worst failure mode."),
						_draft.GapWeight, 0f, 4f);
					_draft.ColWeight = EditorGUILayout.Slider(
						new GUIContent("Colour-edge align (S_col)",
							"Rewards placements whose block boundaries land on strong source colour edges. "
							+ "Speculative and costly — it has to sample the whole fine surface's colours — so it "
							+ "ships at 0 (skipped). Raise it only during tuning if the geometry terms aren't enough."),
						_draft.ColWeight, 0f, 4f);
				}
			}
		}

		private void DrawTaubin()
		{
			_draft.TaubinPasses = EditorGUILayout.IntSlider(
				new GUIContent("Taubin passes",
					"λ/μ umbrella smoothing passes over the marching-cubes isosurface. Affects ONLY the smooth "
					+ "comparison mesh — the blocky voxel output is built from the occupancy grid and is untouched "
					+ "by this. More passes = smoother but softer."),
				_draft.TaubinPasses, 0, 30);
			using (new EditorGUI.IndentLevelScope())
			{
				_draft.TaubinLambda = EditorGUILayout.Slider(
					new GUIContent("λ (shrink)",
						"The shrinking (positive) smoothing step per pass. Larger = more smoothing per pass but "
						+ "more volume loss before μ inflates it back."),
					_draft.TaubinLambda, 0f, 1f);
				_draft.TaubinMu = EditorGUILayout.Slider(
					new GUIContent("μ (inflate)",
						"The inflating (negative) step that counteracts λ's shrinkage each pass. Should exceed λ "
						+ "so the mesh keeps its volume instead of collapsing."),
					_draft.TaubinMu, 0f, 1f);
			}
			_draft.SurfaceReproject = EditorGUILayout.ToggleLeft(
				new GUIContent("SDF surface reprojection",
					"After smoothing, nudge each vertex back onto the SDF iso=0 surface along the gradient — "
					+ "recovers detail the smoothing rounded off. Affects only the smooth comparison mesh, not the "
					+ "blocky output."),
				_draft.SurfaceReproject);
		}

		private void DrawColour()
		{
			_draft.UvDilate = EditorGUILayout.ToggleLeft(
				new GUIContent("UV island dilation",
					"At load, flood each UV island's colours outward into the surrounding texture gutter so a "
					+ "nearest-surface sample can't land on Meshy's purple UV-gutter bleed. Rebuilds the texture "
					+ "snapshot; the mesh itself is untouched."),
				_draft.UvDilate);
			if (_draft.UvDilate)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					_draft.UvDilatePasses = EditorGUILayout.IntSlider(
						new GUIContent("Passes",
							"How many texels of reach to flood island colour into the gutter (one 8-neighbour "
							+ "dilation pass = one texel). 8 is plenty for typical bleed; raise it for wide gutters."),
						_draft.UvDilatePasses, 1, 32);
				}
			}

			_draft.MultiSampleColour = EditorGUILayout.ToggleLeft(
				new GUIContent("Multi-sample voxel colour",
					"Colour each surface voxel from the centre plus several jittered samples per exposed face, then "
					+ "take the Oklab medoid (the sample closest to all the others). A lone stray texel or AO "
					+ "speckle loses the vote instead of tinting an average. Off = one centre sample per voxel."),
				_draft.MultiSampleColour);

			_draft.ColourMode = (ColourMode)EditorGUILayout.EnumPopup(
				new GUIContent("Colour mode",
					"Raw: the reprojected colours untouched (truest read). Per-model palette: cluster them down to "
					+ "a fixed few colours with Oklab k-means (the Crossy-Road flat-colour look). Master palette: "
					+ "snap each to the nearest swatch of a shared palette for cross-asset cohesion. Consolidated: "
					+ "keep Raw's faithful colours but merge near-identical shades into the model's fundamental "
					+ "colours (emergent count, driven by a tolerance rather than a fixed target)."),
				_draft.ColourMode);
			using (new EditorGUI.IndentLevelScope())
			{
				switch (_draft.ColourMode)
				{
					case ColourMode.PerModelPalette:
						_draft.PaletteSize = EditorGUILayout.IntSlider(
							new GUIContent("Palette size",
								"Target number of colours to cluster the model down to. Fewer = flatter, more "
								+ "stylised; empty clusters are dropped, so the actual count may come out lower."),
							_draft.PaletteSize, 2, 32);
						break;
					case ColourMode.MasterPalette:
						_masterPalette = (VoxMasterPalette?)EditorGUILayout.ObjectField(
							new GUIContent("Master palette",
								"The shared swatch set to snap every colour to (Oklab nearest, with a chroma-gain "
								+ "penalty so neutrals don't turn saturated). Empty = the built-in starter palette."),
							_masterPalette, typeof(VoxMasterPalette), false);
						if (_masterPalette == null)
						{
							EditorGUILayout.LabelField(" ", "Using the built-in starter palette.", EditorStyles.miniLabel);
						}

						break;
					case ColourMode.Consolidated:
						_draft.ConsolidateTolerance = EditorGUILayout.Slider(
							new GUIContent("Merge tolerance",
								"How close two shades must be (Oklab distance) to collapse into one fundamental "
								+ "colour. Raw scatters a solid region across dozens of near-duplicate shades; this "
								+ "merges them while leaving genuinely distinct colours apart. 0 = exact (no merge, "
								+ "same as Raw); raise it to fold more variation together. ~0.05–0.08 removes texture "
								+ "noise without blurring real regions; too high and distinct colours merge."),
							_draft.ConsolidateTolerance, 0f, 0.3f);
						_draft.ConsolidateMaxColours = EditorGUILayout.IntSlider(
							new GUIContent("Max colours",
								"Hard cap on the output colour count — locks the model to a known number. After the "
								+ "tolerance merge, the nearest colours keep merging (frequency-weighted, so the "
								+ "dominant shades survive — not the chromatic outliers a fixed-palette k-means "
								+ "chases) until at most this many remain. Set it to the source image's palette size "
								+ "(e.g. 5 for a 5-colour reference) to reproduce it. 0 = unlimited (tolerance only). "
								+ "Fewer distinct colours than the cap yields fewer — it never invents colours."),
							_draft.ConsolidateMaxColours, 0, 32);
						break;
				}
			}

			using (new EditorGUI.DisabledScope(_draft.ColourMode == ColourMode.Raw))
			{
				_draft.PottsStrength = EditorGUILayout.Slider(
					new GUIContent("Potts smoothing",
						"Edge-aware label smoothing after palette assignment: relabels each voxel toward its "
						+ "neighbours' colour, but the penalty melts away where the source colours genuinely "
						+ "disagree — so it erases AO-speckle faux-gradients while pinning real region boundaries. "
						+ "The knob is normalised across models; 0 = off. Needs a palette (not Raw mode)."),
					_draft.PottsStrength, 0f, 2f);
			}

			_draft.NormalConsistency = EditorGUILayout.ToggleLeft(
				new GUIContent("Normal-consistency reject",
					"On a thin wall the nearest triangle can be the back face, whose texels carry the interior/AO "
					+ "colour. This discards a sampled colour whose triangle faces away from the outward SDF "
					+ "gradient, falling back to the flat colour. Heuristic; off by default."),
				_draft.NormalConsistency);
		}
	}
}
