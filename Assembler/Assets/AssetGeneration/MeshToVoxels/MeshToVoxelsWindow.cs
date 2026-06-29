using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels
{
	/// <summary>
	/// Editor window for the mesh → voxel conversion: pick a textured mesh (.obj or .fbx),
	/// choose a resolution, solid-fill it into a coloured MagicaVoxel <c>.vox</c>, then run the
	/// post-processing pipeline (<see cref="VoxModel"/>) over the dense <see cref="VoxPipelinePreset"/>.
	/// The pipeline is driven by a category <see cref="VoxPipelineSettings"/> whose
	/// <see cref="VoxPipeline"/> the per-step toggles below override.
	/// </summary>
	public sealed class MeshToVoxelsWindow : EditorWindow
	{
		private const string DefaultPaletteAssetPath = "Assets/VoxelPipeline/MasterPalette.asset";
		private const string PrefPrefix = "MeshToVoxels.";

		private string _objPath = "";
		private string _voxDir = "";
		private string _voxFile = "";
		private int _maxDimVoxels = 32;
		private bool _converting;

		[SerializeField] private VoxPipelinePreset _preset = VoxPipelinePreset.Creature;
		[SerializeField] private VoxPipelineSettings _settings = VoxPipelinePresets.For(VoxPipelinePreset.Creature);
		[SerializeField] private VoxMasterPalette? _palette;

		[MenuItem("Window/Voxels/Mesh to Voxels")]
		private static void Open() => GetWindow<MeshToVoxelsWindow>("Mesh to Voxels");

		private void OnEnable() => LoadState();

		private void OnDisable() => SaveState();

		private void OnGUI()
		{
			EditorGUILayout.HelpBox(
				"Solid-fills a textured .obj or .fbx into a coloured MagicaVoxel .vox " +
				"using a fast-winding-number occupancy test, then cleans it up.",
				MessageType.Info);

			EditorGUILayout.Space();

			// Mesh source.
			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField("Mesh", GUILayout.Width(40));
				EditorGUILayout.SelectableLabel(
					string.IsNullOrEmpty(_objPath) ? "(none selected)" : _objPath,
					EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
				if (GUILayout.Button("Browse…", GUILayout.Width(80)))
				{
					string picked = EditorUtility.OpenFilePanel("Select mesh", "", "obj,fbx");
					if (!string.IsNullOrEmpty(picked))
					{
						_objPath = picked;
						if (string.IsNullOrEmpty(_voxDir))
						{
							_voxDir = Path.GetDirectoryName(picked) ?? Application.dataPath;
						}
					}
				}
			}

			EditorGUILayout.Space();

			// Resolution.
			_maxDimVoxels = EditorGUILayout.IntSlider(
				new GUIContent("Max dimension (voxels)", "Longest bounding-box axis gets this many voxels; the others scale proportionally."),
				_maxDimVoxels, 1, 256);
			if (_maxDimVoxels >= 96)
			{
				EditorGUILayout.HelpBox(
					"High resolutions run millions of winding-number queries and can take a while (synchronous).",
					MessageType.Warning);
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Post-processing", EditorStyles.boldLabel);

			DrawPipelineControls();

			EditorGUILayout.Space();

			// Output.
			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField("VOX dir", GUILayout.Width(60));
				EditorGUILayout.SelectableLabel(
					string.IsNullOrEmpty(_voxDir) ? "(none)" : _voxDir,
					EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
				if (GUILayout.Button("Browse…", GUILayout.Width(80)))
				{
					string startDir = string.IsNullOrEmpty(_voxDir)
						? (string.IsNullOrEmpty(_objPath) ? Application.dataPath : Path.GetDirectoryName(_objPath) ?? Application.dataPath)
						: _voxDir;
					string picked = EditorUtility.OpenFolderPanel("Save VOX into", startDir, "");
					if (!string.IsNullOrEmpty(picked))
					{
						_voxDir = picked;
					}
				}
			}

			_voxFile = EditorGUILayout.TextField(
				new GUIContent("VOX file", "Leave blank to use the source mesh's filename. A .vox extension is added if missing."),
				_voxFile);

			EditorGUILayout.Space();

			using (new EditorGUI.DisabledScope(
				_converting || string.IsNullOrEmpty(_objPath) || string.IsNullOrEmpty(_voxDir)))
			{
				if (GUILayout.Button(_converting ? "Converting…" : "Convert", GUILayout.Height(32)))
				{
					Convert();
				}
			}
		}

		/// <summary>
		/// Preset picker + per-step overrides. Choosing a preset loads its settings; the toggles
		/// below then act as the per-asset override on top (§4.3). "Reset to preset" re-applies it.
		/// </summary>
		private void DrawPipelineControls()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				var newPreset = (VoxPipelinePreset)EditorGUILayout.EnumPopup(
					new GUIContent("Preset", "Category starting point. Selecting one loads its step settings, which you can then tweak below."),
					_preset);
				if (newPreset != _preset)
				{
					_preset = newPreset;
					_settings = VoxPipelinePresets.For(_preset);
				}
				if (GUILayout.Button("Reset to preset", GUILayout.Width(120)))
				{
					_settings = VoxPipelinePresets.For(_preset);
				}
			}

			EditorGUILayout.Space();

			// Step 1a — native-pitch master (A2). For voxel-style meshes: voxelize a lossless master at the
			// mesh's own baked pitch, then resample to the target in voxel space so non-power-of-2 targets
			// stay crisp. Auto-falls back to the direct/supersample path when the mesh isn't grid-like.
			_settings.nativePitchMaster = EditorGUILayout.ToggleLeft(
				new GUIContent("Native-pitch master (voxel-style meshes)",
					"Voxelize a lossless master at the mesh's baked voxel pitch, then resample in voxel space — keeps any target resolution crisp for voxel-art meshes. Falls back automatically for smooth/organic meshes."),
				_settings.nativePitchMaster);
			if (_settings.nativePitchMaster)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					_settings.nativePitchConfidence = EditorGUILayout.Slider(
						new GUIContent("Min confidence", "Min lattice-fit confidence to take the master path. Below this the mesh is treated as non-voxel and the direct/supersample path runs. Higher = stricter."),
						_settings.nativePitchConfidence, 0f, 1f);
				}
			}

			// Step 1b — supersample-and-downres (detail preservation). The non-voxel-mesh detail path; also
			// the fallback when the master path's detection isn't confident. Off by default.
			_settings.supersample = EditorGUILayout.ToggleLeft(
				new GUIContent("Supersample (preserve detail)",
					"Voxelize at a higher resolution then downres to the target, preserving thin features and small colour details that direct low-res voxelization aliases away."),
				_settings.supersample);
			if (_settings.supersample)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					_settings.supersampleFactor = EditorGUILayout.IntSlider(
						new GUIContent("Factor", "Voxelize at this multiple of the target dimension. Each output voxel aggregates a factor³ block — higher preserves more but is much slower (factor³ work)."),
						_settings.supersampleFactor, 2, 4);
					EditorGUILayout.HelpBox(
						$"Voxelizes at up to {_settings.supersampleFactor}× the dimension " +
						$"({_settings.supersampleFactor * _settings.supersampleFactor * _settings.supersampleFactor}× the cells) before downres — slower.",
						MessageType.Info);
				}
			}

			// Shared occupancy/colour levers for both detail paths (supersample downres and master resample).
			if (_settings.supersample || _settings.nativePitchMaster)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					_settings.downresCoverageThreshold = EditorGUILayout.Slider(
						new GUIContent("Coverage threshold", "Fill an output voxel when this fraction of its high-res block (or master support) was occupied. Lower = fatter; higher = leaner."),
						_settings.downresCoverageThreshold, 0f, 1f);
					_settings.downresFeatureAware = EditorGUILayout.ToggleLeft(
						new GUIContent("Feature-aware", "Force-keep features thinner than one output voxel (antennae, fins) that the coverage vote would erase."),
						_settings.downresFeatureAware);
					_settings.downresColourSalience = EditorGUILayout.Slider(
						new GUIContent("Colour salience", "Boost perceptually distinct minority colours when collapsing, so small details aren't outvoted into mush. 0 = pure majority."),
						_settings.downresColourSalience, 0f, 5f);
				}
			}

			// Step 2 — floaters.
			_settings.removeFloaters = EditorGUILayout.ToggleLeft(
				new GUIContent("Remove floaters",
					"Delete small disconnected components (voxelization specks). Substantial detached parts are kept."),
				_settings.removeFloaters);
			if (_settings.removeFloaters)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					_settings.floaterMinPercent = EditorGUILayout.Slider(
						new GUIContent("Min component %", "A component covering less than this % of voxels (and < 2 voxels) is removed."),
						_settings.floaterMinPercent, 0f, 10f);
				}
			}

			// Step 3 — symmetry (opt-in). Both off by default: forcing symmetry erases intentional asymmetry.
			_settings.mirror = EditorGUILayout.ToggleLeft(
				new GUIContent("Mirror (force symmetry)",
					"Mirror one half about a plane onto the other. Off by default — erases intentional asymmetry (eyepatch, raised paw)."),
				_settings.mirror);
			if (_settings.mirror)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					_settings.mirrorAxis = (SymmetryAxis)EditorGUILayout.EnumPopup(
						new GUIContent("Mirror axis", "Axis the mirror plane is perpendicular to. Left/right (X) is the usual bilateral plane."),
						_settings.mirrorAxis);
					_settings.mirrorConfidence = EditorGUILayout.Slider(
						new GUIContent("Confidence gate", "Min mirror-overlap score to auto-apply. Below this the model is treated as not symmetric and left as-is."),
						_settings.mirrorConfidence, 0f, 1f);
					_settings.mirrorForce = EditorGUILayout.ToggleLeft(
						new GUIContent("Force past gate", "Apply at the best-scoring plane even when the confidence gate fails (for a stubborn asset)."),
						_settings.mirrorForce);
				}
			}

			_settings.revolve = EditorGUILayout.ToggleLeft(
				new GUIContent("Revolve (force roundness)",
					"Revolve the radial profile into a true solid of revolution. Off by default — for standalone wheels/cylinders only."),
				_settings.revolve);
			if (_settings.revolve)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					_settings.revolveAxis = (SymmetryAxis)EditorGUILayout.EnumPopup(
						new GUIContent("Spin axis", "Axis the profile is revolved about. Up (Y) is the usual wheel axle."),
						_settings.revolveAxis);
					_settings.revolveFillThreshold = EditorGUILayout.Slider(
						new GUIContent("Ring fill threshold", "A ring is filled when at least this fraction of its cells were occupied."),
						_settings.revolveFillThreshold, 0f, 1f);
				}
			}

			// Step 4 — de-light.
			_settings.deLight = EditorGUILayout.ToggleLeft(
				new GUIContent("De-light",
					"Flatten baked shading: grow material regions of similar colour and collapse each to one flat colour."),
				_settings.deLight);
			if (_settings.deLight)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					_settings.deLightThreshold = EditorGUILayout.Slider(
						new GUIContent("Region similarity (Oklab)", "Max perceptual distance between adjacent voxels to join one region. Higher = larger, flatter regions."),
						_settings.deLightThreshold, 0f, 0.5f);
				}
			}

			// Step 5a — histogram-peak snap (per-model colour reduction, before the shared-palette snap).
			_settings.snapToHistogramPeaks = EditorGUILayout.ToggleLeft(
				new GUIContent("Snap to histogram peaks",
					"Reduce to the model's own dominant colours: snap every voxel to a variety-selected set of peaks in its colour histogram, spread out perceptually rather than just the most common. Runs before the master-palette snap."),
				_settings.snapToHistogramPeaks);
			if (_settings.snapToHistogramPeaks)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					_settings.histogramPeakVariety = EditorGUILayout.Slider(
						new GUIContent("Variety threshold (Oklab)", "Keep adding peaks while each new colour is at least this distinct from the ones already kept; stop when the next-best is closer. Higher = fewer, more distinct colours. This is the primary control."),
						_settings.histogramPeakVariety, 0f, 0.5f);
					_settings.histogramPeakCount = EditorGUILayout.IntSlider(
						new GUIContent("Max peaks (cap)", "Upper bound on how many distinct colours to keep. Selection usually stops earlier, once no colour clears the variety threshold."),
						_settings.histogramPeakCount, 1, 64);
				}
			}

			// Step 5b — palette-snap.
			_settings.snapToPalette = EditorGUILayout.ToggleLeft(
				new GUIContent("Snap to master palette",
					"Snap each colour to the nearest swatch in a shared master palette (Oklab) for cross-asset cohesion."),
				_settings.snapToPalette);
			if (_settings.snapToPalette)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					_palette = (VoxMasterPalette?)EditorGUILayout.ObjectField(
						new GUIContent("Master palette", "Hand-authored swatches. Leave empty to use the built-in starter palette."),
						_palette, typeof(VoxMasterPalette), false);
					if (_palette == null)
					{
						using (new EditorGUILayout.HorizontalScope())
						{
							EditorGUILayout.LabelField("Using built-in starter palette.", EditorStyles.miniLabel);
							if (GUILayout.Button("Create starter palette…", GUILayout.Width(170)))
							{
								_palette = CreateStarterPalette();
							}
						}
					}
				}
			}

			// Step 6 — morphology.
			_settings.morphology = EditorGUILayout.ToggleLeft(
				new GUIContent("Despeckle / fill (morphology)",
					"Mild: remove single-face bumps and fill near-enclosed pinholes. Best left off for organic models — can erode thin features."),
				_settings.morphology);
		}

		// async void: a UI event handler that can't return a Task. The whole body is wrapped in
		// try/catch (per house style) so an exception can't escape unhandled, and the conversion
		// itself runs off the main thread (VoxConversion.Run) so the editor stays responsive.
		private async void Convert()
		{
			if (_converting)
			{
				return;
			}

			try
			{
				if (!File.Exists(_objPath))
				{
					EditorUtility.DisplayDialog("Mesh → VOX", $"Mesh not found:\n{_objPath}", "OK");
					return;
				}

				_converting = true;
				string voxPath = ResolveVoxPath();
				var palette = _palette != null ? _palette.ToColor32() : DefaultMasterPalette.Colors;
				VoxConversion.Summary summary = await VoxConversion.Run(
					_objPath, voxPath, _maxDimVoxels, _settings, palette,
					new EditorProgressReporter(),
					(name, fraction) =>
						EditorUtility.DisplayProgressBar("Mesh → VOX", $"Post-processing: {name}…", 0.9f + 0.09f * fraction));

				Debug.Log($"[MeshToVoxels] Wrote {summary}");
			}
			catch (OperationCanceledException)
			{
				EditorUtility.DisplayDialog("Mesh → VOX", "Conversion cancelled.", "OK");
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				EditorUtility.DisplayDialog("Mesh → VOX", $"Conversion failed:\n{e.Message}", "OK");
			}
			finally
			{
				_converting = false;
				EditorUtility.ClearProgressBar();
				Repaint();
			}
		}

		private static VoxMasterPalette CreateStarterPalette()
		{
			var palette = CreateInstance<VoxMasterPalette>();
			palette.SetColors(DefaultMasterPalette.Colors);

			string dir = Path.GetDirectoryName(DefaultPaletteAssetPath)!;
			if (!Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}
			string path = AssetDatabase.GenerateUniqueAssetPath(DefaultPaletteAssetPath);
			AssetDatabase.CreateAsset(palette, path);
			AssetDatabase.SaveAssets();
			EditorGUIUtility.PingObject(palette);
			return palette;
		}

		// ---- EditorPrefs persistence ----------------------------------------
		// All window settings are cached so the next session reopens where you left
		// off. Saved on close/domain-reload (OnDisable), restored on OnEnable.

		private void LoadState()
		{
			_objPath = EditorPrefs.GetString(PrefPrefix + "MeshPath", _objPath);
			_voxDir = EditorPrefs.GetString(PrefPrefix + "VoxDir", _voxDir);
			_voxFile = EditorPrefs.GetString(PrefPrefix + "VoxFile", _voxFile);
			_maxDimVoxels = EditorPrefs.GetInt(PrefPrefix + "MaxDim", _maxDimVoxels);
			_preset = (VoxPipelinePreset)EditorPrefs.GetInt(PrefPrefix + "Preset", (int)_preset);

			string settingsJson = EditorPrefs.GetString(PrefPrefix + "Settings", "");
			if (!string.IsNullOrEmpty(settingsJson))
			{
				JsonUtility.FromJsonOverwrite(settingsJson, _settings);
			}

			string paletteGuid = EditorPrefs.GetString(PrefPrefix + "PaletteGuid", "");
			if (!string.IsNullOrEmpty(paletteGuid))
			{
				string path = AssetDatabase.GUIDToAssetPath(paletteGuid);
				_palette = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<VoxMasterPalette>(path);
			}

			if (!string.IsNullOrEmpty(_objPath) && string.IsNullOrEmpty(_voxDir))
			{
				_voxDir = Path.GetDirectoryName(_objPath) ?? Application.dataPath;
			}
		}

		private void SaveState()
		{
			EditorPrefs.SetString(PrefPrefix + "MeshPath", _objPath);
			EditorPrefs.SetString(PrefPrefix + "VoxDir", _voxDir);
			EditorPrefs.SetString(PrefPrefix + "VoxFile", _voxFile);
			EditorPrefs.SetInt(PrefPrefix + "MaxDim", _maxDimVoxels);
			EditorPrefs.SetInt(PrefPrefix + "Preset", (int)_preset);
			EditorPrefs.SetString(PrefPrefix + "Settings", JsonUtility.ToJson(_settings));

			string assetPath = _palette != null ? AssetDatabase.GetAssetPath(_palette) : "";
			EditorPrefs.SetString(
				PrefPrefix + "PaletteGuid",
				string.IsNullOrEmpty(assetPath) ? "" : AssetDatabase.AssetPathToGUID(assetPath));
		}

		// Combine the chosen directory with the file name; a blank name falls back to
		// the source mesh's basename, and a missing extension defaults to .vox.
		private string ResolveVoxPath()
		{
			string name = string.IsNullOrWhiteSpace(_voxFile)
				? Path.GetFileNameWithoutExtension(_objPath) + ".vox"
				: _voxFile.Trim();
			if (!Path.HasExtension(name))
			{
				name += ".vox";
			}

			string dir = string.IsNullOrEmpty(_voxDir)
				? (Path.GetDirectoryName(_objPath) ?? Application.dataPath)
				: _voxDir;
			return Path.Combine(dir, name);
		}

		private sealed class EditorProgressReporter : IProgressReporter
		{
			public bool Report(float fraction, string message) =>
				!EditorUtility.DisplayCancelableProgressBar("Mesh → VOX", message, fraction);
		}
	}
}
