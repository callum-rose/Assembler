using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Assembler.AssetGeneration.EditorCommon;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxel.Editor
{
	/// <summary>
	/// Editor window for the SDF-remesh + colour-reprojection mesh → voxel path. Pick a messy Meshy
	/// mesh (.obj/.fbx), tune the resolution / placement-search / cleanup / colour passes — every
	/// pass individually toggleable — and Convert to reveal every intermediate stage side by side in
	/// the scene, with the Crossy-Road blocky voxel model as the primary output and an objective
	/// metrics readout per run. "Run test set…" batch-runs a locked folder of meshes for the
	/// consistency eval. Isolated from the existing <c>Window > Voxels > Mesh to Voxels</c> path so
	/// the two can be A/B compared on the same asset.
	/// </summary>
	public sealed class MeshToVoxelWindow : EditorWindow
	{
		private const string PrefPrefix = "MeshToVoxel.";

		private string _meshPath = "";
		private string _testSetFolder = "";

		// The full mesh → voxel control set, shared with the Text → Voxels pipeline window.
		private readonly VoxSettingsGui _vox = new();

		private bool _revealIntermediates = true;
		private float _rowSpacing = 1f;

		private bool _converting;
		private Vector2 _scroll;

		private readonly List<TestSetRunner.Entry> _lastEntries = new();
		private string _lastCsv = "";

		[MenuItem("Assembler/Voxelisation/Mesh to Voxel")]
		private static void Open() => GetWindow<MeshToVoxelWindow>("Mesh → Voxel");

		private void OnEnable() => LoadState();

		private void OnDisable() => SaveState();

		private void OnGUI()
		{
			_scroll = EditorGUILayout.BeginScrollView(_scroll);
			EditorGUILayout.HelpBox(
				"Remeshes a messy .obj/.fbx via a generalized-winding-number SDF + marching cubes, votes the "
				+ "occupancy onto a searched grid placement, cleans it up, then reprojects colour from the "
				+ "original surface and flattens to a smoothed palette — producing a Crossy-Road blocky voxel "
				+ "model plus a smooth comparison.",
				MessageType.Info);

			EditorGUILayout.Space();
			DrawMeshPicker();

			EditorGUILayout.Space();
			_vox.Draw();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
			_revealIntermediates = EditorGUILayout.ToggleLeft(
				new GUIContent("Reveal intermediates",
					"On: lay the full progression out along +X — original → marching-cubes isosurface → Taubin "
					+ "smoothed → (SDF reprojected) → smooth reprojected-colour → blocky voxel model — for A/B "
					+ "judgement. Off: show only the primary blocky voxel model."),
				_revealIntermediates);
			using (new EditorGUI.DisabledScope(!_revealIntermediates))
			{
				_rowSpacing = EditorGUILayout.Slider(
					new GUIContent("Row spacing",
						"Multiplier on the gap between stages in the preview row (scaled by each mesh's size). "
						+ "Raise it if adjacent stages overlap. Only applies when intermediates are revealed."),
					_rowSpacing, 0.25f, 4f);
			}

			EditorGUILayout.Space();
			using (new EditorGUI.DisabledScope(_converting || string.IsNullOrEmpty(_meshPath)))
			{
				if (GUILayout.Button(
					new GUIContent(_converting ? "Converting…" : "Convert",
						"Run the pipeline on the selected mesh with the settings above and show the result in the "
						+ "scene. Runs synchronously on the main thread — the editor blocks until it finishes."),
					GUILayout.Height(32)))
				{
					Convert(export: false);
				}
				if (GUILayout.Button(
					new GUIContent(_converting ? "Converting…" : "Convert & Save .vox…",
						"Run the pipeline and additionally write the blocky occupancy grid out as a MagicaVoxel "
						+ ".vox at a path you pick. Asks for the destination before the (slow) run."),
					GUILayout.Height(32)))
				{
					Convert(export: true);
				}
			}
			using (new EditorGUI.DisabledScope(_converting))
			{
				if (GUILayout.Button(
					new GUIContent("Run test set…",
						"Batch-run the pipeline over every .obj/.fbx in a folder you pick (non-recursive), with "
						+ "these same settings. Stacks one preview row per mesh and fills the metrics panel + CSV "
						+ "for the consistency eval. Failures are logged and skipped."),
					GUILayout.Height(24)))
				{
					RunTestSet();
				}
			}

			DrawMetrics();
			EditorGUILayout.EndScrollView();
		}

		private void DrawMeshPicker()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField(
					new GUIContent("Mesh", "The source .obj/.fbx to voxelise — typically a messy textured Meshy export. Drag a mesh asset onto this row, or browse for a file."),
					GUILayout.Width(40));
				EditorGUILayout.SelectableLabel(
					string.IsNullOrEmpty(_meshPath) ? "(none selected)" : _meshPath,
					EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
				if (GUILayout.Button(
					new GUIContent("Browse…", "Pick the source mesh (.obj or .fbx) from disk. The choice is remembered between sessions."),
					GUILayout.Width(80)))
				{
					string picked = EditorUtility.OpenFilePanel("Select mesh", PathField.GuessStartDir(_meshPath), "obj,fbx");
					if (!string.IsNullOrEmpty(picked))
					{
						_meshPath = picked;
					}
				}
			}

			_meshPath = PathField.HandleDrop(GUILayoutUtility.GetLastRect(), _meshPath);
		}

		private void DrawMetrics()
		{
			if (_lastEntries.Count == 0)
			{
				return;
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Metrics", EditorStyles.boldLabel);
			foreach (TestSetRunner.Entry entry in _lastEntries)
			{
				EditorGUILayout.LabelField(entry.Name, EditorStyles.miniBoldLabel);
				EditorGUILayout.LabelField(entry.Metrics.ToLogString(), EditorStyles.wordWrappedMiniLabel);
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button(
					new GUIContent("Copy CSV",
						"Copy the metrics for the last run / test set to the clipboard as CSV (one row per mesh, "
						+ "with a header) — paste it into a spreadsheet to compare runs.")))
				{
					EditorGUIUtility.systemCopyBuffer = _lastCsv;
					ShowNotification(new GUIContent("Metrics CSV copied"));
				}
				if (GUILayout.Button(
					new GUIContent("Log metrics", "Print each mesh's metrics line to the Console.")))
				{
					foreach (TestSetRunner.Entry entry in _lastEntries)
					{
						Debug.Log($"[MeshToVoxel] {entry.Name}: {entry.Metrics.ToLogString()}");
					}
				}
			}
		}

		// async void: a UI event handler that can't return a Task. The whole body is wrapped in
		// try/catch (house style) so an exception can't escape unhandled. When export is true the run
		// additionally writes the blocky occupancy grid out as a .vox at a user-picked path.
		private async void Convert(bool export)
		{
			if (_converting)
			{
				return;
			}

			try
			{
				if (!File.Exists(_meshPath))
				{
					EditorUtility.DisplayDialog("Mesh → Voxel", $"Mesh not found:\n{_meshPath}", "OK");
					return;
				}

				// Ask for the destination before the slow run so a cancelled save panel costs nothing.
				string voxPath = "";
				if (export)
				{
					voxPath = EditorUtility.SaveFilePanel(
						"Save .vox", Path.GetDirectoryName(_meshPath) ?? "",
						Path.GetFileNameWithoutExtension(_meshPath), "vox");
					if (string.IsNullOrEmpty(voxPath))
					{
						return;
					}
				}

				_converting = true;
				Repaint();
				// Let the "Converting…" button state paint before the synchronous run blocks the thread.
				await Task.Yield();

				Settings settings = _vox.ToSettings();
				StageResult result = Pipeline.Run(
					_meshPath, settings,
					(fraction, stage) => EditorUtility.DisplayProgressBar("Mesh → Voxel", stage + "…", fraction));

				if (_revealIntermediates)
				{
					StagePreviewer.Show(result, _rowSpacing);
				}
				else
				{
					StagePreviewer.ShowBlockyOnly(result);
				}

				string name = Path.GetFileNameWithoutExtension(_meshPath);
				_lastEntries.Clear();
				_lastEntries.Add(new TestSetRunner.Entry { Name = name, Metrics = result.Metrics });
				_lastCsv = TestSetRunner.BuildCsv(_lastEntries);
				Debug.Log($"[MeshToVoxel] {name}: {result.Metrics.ToLogString()}");

				if (export)
				{
					int written = VoxExport.Write(voxPath, result.Occupancy, result.VoxelColours);
					AssetPaths.RefreshIfInside(voxPath);
					EditorUtility.DisplayDialog(
						"Mesh → Voxel",
						$"Wrote {written:N0} voxels ({result.GridX}×{result.GridY}×{result.GridZ}) to:\n{voxPath}", "OK");
				}
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				EditorUtility.DisplayDialog("Mesh → Voxel", $"Conversion failed:\n{e.Message}", "OK");
			}
			finally
			{
				_converting = false;
				EditorUtility.ClearProgressBar();
				Repaint();
			}
		}

		// async void event handler; body wrapped in try/catch per house style.
		private async void RunTestSet()
		{
			if (_converting)
			{
				return;
			}

			try
			{
				string folder = EditorUtility.OpenFolderPanel(
					"Select test-set folder",
					string.IsNullOrEmpty(_testSetFolder) ? "" : _testSetFolder, "");
				if (string.IsNullOrEmpty(folder))
				{
					return;
				}
				_testSetFolder = folder;

				_converting = true;
				Repaint();
				await Task.Yield();

				TestSetRunner.BatchResult batch = TestSetRunner.Run(folder, _vox.ToSettings(), _rowSpacing);
				_lastEntries.Clear();
				_lastEntries.AddRange(batch.Entries);
				_lastCsv = batch.Csv;
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				EditorUtility.DisplayDialog("Mesh → Voxel", $"Test-set run failed:\n{e.Message}", "OK");
			}
			finally
			{
				_converting = false;
				EditorUtility.ClearProgressBar();
				Repaint();
			}
		}

		// ---- EditorPrefs persistence ----------------------------------------

		private void LoadState()
		{
			_meshPath = EditorPrefs.GetString(PrefPrefix + "MeshPath", _meshPath);
			_testSetFolder = EditorPrefs.GetString(PrefPrefix + "TestSetFolder", _testSetFolder);
			_revealIntermediates = EditorPrefs.GetBool(PrefPrefix + "Reveal", _revealIntermediates);
			_rowSpacing = EditorPrefs.GetFloat(PrefPrefix + "RowSpacing", _rowSpacing);
			_vox.Load(PrefPrefix + "Vox");
		}

		private void SaveState()
		{
			EditorPrefs.SetString(PrefPrefix + "MeshPath", _meshPath);
			EditorPrefs.SetString(PrefPrefix + "TestSetFolder", _testSetFolder);
			EditorPrefs.SetBool(PrefPrefix + "Reveal", _revealIntermediates);
			EditorPrefs.SetFloat(PrefPrefix + "RowSpacing", _rowSpacing);
			_vox.Save(PrefPrefix + "Vox");
		}
	}
}
