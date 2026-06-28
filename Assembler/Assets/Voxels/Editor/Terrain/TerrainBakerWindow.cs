using System;
using System.IO;
using System.Text;
using Assembler.Voxels.Scripting;
using Assembler.Voxels.Terrain;
using UnityEditor;
using UnityEngine;

namespace Assembler.Voxels.Editor.Terrain
{
	/// <summary>
	/// Editor front-end for the terrain pipeline: load a <c>*.terrain.yaml</c>, tweak
	/// the high-level fields, live-preview the imported mesh, and bake a <c>.vox</c>
	/// into <c>Resources/Voxels</c>. Mirrors <c>VoxelGeneratorWindow</c>'s scratch-vox
	/// preview path (write → refresh → load imported Mesh → interactive preview).
	/// </summary>
	public sealed class TerrainBakerWindow : EditorWindow
	{
		private const string YamlPathPref = "Assembler.Terrain.LastYamlPath";
		private const string OutputFolderPref = "Assembler.Terrain.OutputFolder";
		private const string VoxelCapPref = "Assembler.Terrain.VoxelCap";
		private const string TimeoutPref = "Assembler.Terrain.TimeoutSeconds";
		private const string ScratchFolder = "Assets/Resources/Voxels/_TerrainPreview/";
		private const string ScratchName = "terrain_preview";
		private const float PreviewWidth = 320f;

		private string _yamlPath = string.Empty;
		private string _outputFolder = TerrainBaker.DefaultOutputFolder;

		private TerrainSpec _spec;
		private string _name = "terrain";
		private int _seed;
		private int _sizeX = 64;
		private int _sizeY = 64;
		private int _sizeZ = 32;
		private Enclosure _enclosure = Enclosure.Open;

		private int _voxelCap = 2_000_000;
		private float _timeoutSeconds = 10f;

		private VoxelModel _lastModel;
		private readonly StringBuilder _log = new();
		private Vector2 _outerScroll;
		private Vector2 _logScroll;

		private string _previewMeshPath;
		private Mesh _previewMesh;
		private UnityEditor.Editor _previewEditor;
		private GUIStyle _richLabelStyle;
		private GUIStyle _logStyle;

		[MenuItem("Assembler/Generate Terrain")]
		public static void Open()
		{
			var window = GetWindow<TerrainBakerWindow>("Generate Terrain");
			window.minSize = new Vector2(720, 480);
			window.Show();
		}

		private void OnEnable()
		{
			_yamlPath = EditorPrefs.GetString(YamlPathPref, string.Empty);
			_outputFolder = EditorPrefs.GetString(OutputFolderPref, TerrainBaker.DefaultOutputFolder);
			_voxelCap = EditorPrefs.GetInt(VoxelCapPref, 2_000_000);
			_timeoutSeconds = EditorPrefs.GetFloat(TimeoutPref, 10f);
		}

		private void OnDisable() => DestroyPreviewEditor();

		private void OnGUI()
		{
			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.BeginVertical();
			_outerScroll = EditorGUILayout.BeginScrollView(_outerScroll);
			DrawControls();
			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();

			EditorGUILayout.BeginVertical(GUILayout.Width(PreviewWidth));
			DrawPreview();
			EditorGUILayout.EndVertical();

			EditorGUILayout.EndHorizontal();
		}

		private void DrawControls()
		{
			EditorGUILayout.LabelField("Recipe", EditorStyles.boldLabel);
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.SelectableLabel(
				string.IsNullOrEmpty(_yamlPath) ? "(no .terrain.yaml loaded)" : _yamlPath,
				EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
			if (GUILayout.Button("Load .terrain.yaml...", GUILayout.Width(160)))
			{
				LoadYaml();
			}

			EditorGUILayout.EndHorizontal();

			using (new EditorGUI.DisabledScope(_spec == null))
			{
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Overrides", EditorStyles.boldLabel);
				_name = EditorGUILayout.TextField("Name", _name);
				_seed = EditorGUILayout.IntField("Seed", _seed);
				_sizeX = EditorGUILayout.IntField("Size X", _sizeX);
				_sizeY = EditorGUILayout.IntField("Size Y", _sizeY);
				_sizeZ = EditorGUILayout.IntField("Size Z (up)", _sizeZ);
				_enclosure = (Enclosure)EditorGUILayout.EnumPopup("Enclosure", _enclosure);

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Limits", EditorStyles.boldLabel);
				using (var scope = new EditorGUI.ChangeCheckScope())
				{
					_voxelCap = EditorGUILayout.IntField("Voxel cap", _voxelCap);
					_timeoutSeconds = EditorGUILayout.FloatField("Timeout (seconds)", _timeoutSeconds);
					if (scope.changed)
					{
						EditorPrefs.SetInt(VoxelCapPref, _voxelCap);
						EditorPrefs.SetFloat(TimeoutPref, _timeoutSeconds);
					}
				}

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Output folder", EditorStyles.boldLabel);
				using (var scope = new EditorGUI.ChangeCheckScope())
				{
					_outputFolder = EditorGUILayout.TextField(_outputFolder);
					if (scope.changed)
					{
						EditorPrefs.SetString(OutputFolderPref, _outputFolder);
					}
				}

				EditorGUILayout.Space();
				if (GUILayout.Button("Preview"))
				{
					Preview();
				}

				if (GUILayout.Button("Bake to Resources"))
				{
					Bake();
				}
			}

			DrawModelSummary();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
			_logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(100), GUILayout.MaxHeight(200));
			_logStyle ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };
			var logText = _log.ToString();
			var logHeight = Mathf.Max(100f, _logStyle.CalcHeight(new GUIContent(logText), EditorGUIUtility.currentViewWidth - 40f));
			EditorGUILayout.SelectableLabel(logText, _logStyle, GUILayout.Height(logHeight), GUILayout.ExpandWidth(true));
			EditorGUILayout.EndScrollView();
		}

		private void DrawPreview()
		{
			EditorGUILayout.LabelField("Mesh preview", EditorStyles.boldLabel);
			RefreshPreviewEditor();

			var rect = GUILayoutUtility.GetRect(PreviewWidth, PreviewWidth, GUILayout.ExpandHeight(true));
			if (_previewEditor != null && _previewMesh != null)
			{
				_previewEditor.OnInteractivePreviewGUI(rect, EditorStyles.helpBox);
			}
			else
			{
				GUI.Box(rect, "No mesh yet — Load then Preview", EditorStyles.helpBox);
			}
		}

		private void LoadYaml()
		{
			var startDir = !string.IsNullOrEmpty(_yamlPath) && File.Exists(_yamlPath)
				? Path.GetDirectoryName(_yamlPath) ?? Application.dataPath
				: Application.dataPath;
			var path = EditorUtility.OpenFilePanel("Load .terrain.yaml", startDir, "yaml");
			if (string.IsNullOrEmpty(path))
			{
				return;
			}

			try
			{
				var spec = TerrainSpecYamlReader.ReadFile(path);
				_spec = spec;
				_yamlPath = path;
				EditorPrefs.SetString(YamlPathPref, _yamlPath);

				_name = spec.Name;
				_seed = spec.Seed;
				_sizeX = spec.Size.x;
				_sizeY = spec.Size.y;
				_sizeZ = spec.Size.z;
				_enclosure = spec.Enclosure;

				Log($"Loaded {Path.GetFileName(path)} ({spec.Ops.Count} modifier op(s)).");
				Preview();
			}
			catch (Exception ex)
			{
				Log("Load failed: " + ex.Message);
			}
		}

		private VoxelScriptLimits CurrentLimits() => new()
		{
			MaxVoxels = Mathf.Max(1, _voxelCap),
			WallClock = TimeSpan.FromSeconds(Mathf.Max(0.5f, _timeoutSeconds)),
		};

		private TerrainSpec EffectiveSpec()
			=> _spec?.With(_name, _seed, new Vector3Int(_sizeX, _sizeY, _sizeZ), _enclosure);

		private void Preview()
		{
			var spec = EffectiveSpec();
			if (spec == null)
			{
				return;
			}

			try
			{
				TerrainBaker.ValidateSize(spec.Size);
				var model = TerrainGenerator.Generate(spec, CurrentLimits());
				_lastModel = model;

				Directory.CreateDirectory(ScratchFolder);
				var scratchPath = Path.Combine(ScratchFolder, ScratchName + ".vox");
				File.WriteAllBytes(scratchPath, VoxWriter.Write(model));
				AssetDatabase.Refresh();

				_previewMeshPath = scratchPath;
				_previewMesh = null;
				DestroyPreviewEditor();
				Log($"Previewed: {model.Voxels.Count} voxels, size {model.Size.x}x{model.Size.y}x{model.Size.z}.");
				WarnIfNearCap(model);
			}
			catch (Exception ex)
			{
				Log("Preview failed: " + ex.Message);
			}
		}

		private void Bake()
		{
			var spec = EffectiveSpec();
			if (spec == null)
			{
				return;
			}

			try
			{
				var path = TerrainBaker.Bake(spec, CurrentLimits(), _outputFolder);
				Log($"Baked {path}. Reference it as Type: mesh, Path: Voxels/{spec.Name}.");
			}
			catch (Exception ex)
			{
				Log("Bake failed: " + ex.Message);
			}
		}

		private void WarnIfNearCap(VoxelModel model)
		{
			if (model.Voxels.Count > _voxelCap * 0.8f)
			{
				Log($"WARNING: voxel count {model.Voxels.Count} is near the cap of {_voxelCap}.");
			}
		}

		private void DrawModelSummary()
		{
			if (_lastModel == null || _lastModel.Voxels.Count == 0)
			{
				return;
			}

			_richLabelStyle ??= new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };

			var size = _lastModel.Size;
			var sb = new StringBuilder();
			sb.Append("Size ").Append(size.x).Append('x').Append(size.y).Append('x').Append(size.z)
				.Append("  Voxels ").Append(_lastModel.Voxels.Count)
				.Append("  Colours ").Append(_lastModel.Palette.Length).Append("  ");

			foreach (var c in _lastModel.Palette)
			{
				var hex = $"{c.r:x2}{c.g:x2}{c.b:x2}";
				sb.Append("<color=#").Append(hex).Append(">■</color>");
				sb.Append("<color=#888888>#").Append(hex).Append("</color> ");
			}

			EditorGUILayout.LabelField(sb.ToString(), _richLabelStyle);
		}

		private void RefreshPreviewEditor()
		{
			// The Voxel Toolkit import is async, so the imported Mesh may not be
			// available on the first frame after Refresh — retry until it loads.
			if (_previewMesh == null && !string.IsNullOrEmpty(_previewMeshPath))
			{
				_previewMesh = AssetDatabase.LoadAssetAtPath<Mesh>(_previewMeshPath);
			}

			if (_previewMesh != null && _previewEditor == null)
			{
				_previewEditor = UnityEditor.Editor.CreateEditor(_previewMesh);
			}
		}

		private void DestroyPreviewEditor()
		{
			if (_previewEditor != null)
			{
				DestroyImmediate(_previewEditor);
				_previewEditor = null;
			}
		}

		private void Log(string message)
		{
			_log.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ").AppendLine(message);
			Repaint();
		}
	}
}
