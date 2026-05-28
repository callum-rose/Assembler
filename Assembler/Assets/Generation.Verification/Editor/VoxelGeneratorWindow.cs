using System;
using System.IO;
using System.Text;
using System.Threading;
using Assembler.Anthropic;
using Assembler.Voxels;
using UnityEditor;
using UnityEngine;

namespace Assembler.Generation.Verification.Editor
{
	public sealed class VoxelGeneratorWindow : EditorWindow
	{
		private const string ApiKeyPref = "Assembler.Generation.ApiKey";
		private const string PromptPref = "Assembler.Voxels.LastPrompt";
		private const string PersistentInstructionsPref = "Assembler.Voxels.PersistentInstructions";
		private const string NamePref = "Assembler.Voxels.LastName";
		private const string OutputFolderPref = "Assembler.Voxels.OutputFolder";
		private const string DefaultOutputFolder = "Assets/Resources/Voxels/";
		private const string ScratchFolder = "Assets/Resources/Voxels/_Preview/";
		private const string ScratchName = "preview";
		private const float PreviewWidth = 320f;

		private string _apiKey = string.Empty;
		private string _prompt = string.Empty;
		private string _persistentInstructions = string.Empty;
		private string _name = "voxel";
		private string _outputFolder = DefaultOutputFolder;
		private string _goxelText = string.Empty;
		private byte[]? _voxBytes;

		private readonly StringBuilder _log = new();
		private Vector2 _outerScroll;
		private Vector2 _logScroll;
		private Vector2 _promptScroll;
		private Vector2 _persistentScroll;
		private Vector2 _goxelScroll;
		private bool _isRunning;
		private CancellationTokenSource? _cts;

		private string? _previewMeshPath;
		private Mesh? _previewMesh;
		private UnityEditor.Editor? _previewEditor;

		[MenuItem("Assembler/Generate Voxel Mesh")]
		public static void Open()
		{
			var window = GetWindow<VoxelGeneratorWindow>("Generate Voxel");
			window.minSize = new Vector2(720, 500);
			window.Show();
		}

		private void OnEnable()
		{
			_apiKey = EditorPrefs.GetString(ApiKeyPref, string.Empty);
			_prompt = EditorPrefs.GetString(PromptPref, string.Empty);
			_name = EditorPrefs.GetString(NamePref, "voxel");
			_outputFolder = EditorPrefs.GetString(OutputFolderPref, DefaultOutputFolder);
			_persistentInstructions = EditorPrefs.GetString(PersistentInstructionsPref, string.Empty);
		}

		private void OnDisable()
		{
			_cts?.Cancel();
			DestroyPreviewEditor();
		}

		private void OnGUI()
		{
			EditorGUILayout.BeginHorizontal();

			// Left column: scrollable controls.
			EditorGUILayout.BeginVertical();
			_outerScroll = EditorGUILayout.BeginScrollView(_outerScroll);
			DrawControls();
			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();

			// Right column: fixed-width preview pane.
			EditorGUILayout.BeginVertical(GUILayout.Width(PreviewWidth));
			DrawPreview();
			EditorGUILayout.EndVertical();

			EditorGUILayout.EndHorizontal();
		}

		private void DrawControls()
		{
			EditorGUILayout.LabelField("API key (shared with descriptor window)", EditorStyles.boldLabel);
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_apiKey = EditorGUILayout.PasswordField(_apiKey);
				if (scope.changed) EditorPrefs.SetString(ApiKeyPref, _apiKey);
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Output folder", EditorStyles.boldLabel);
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_outputFolder = EditorGUILayout.TextField(_outputFolder);
				if (scope.changed) EditorPrefs.SetString(OutputFolderPref, _outputFolder);
			}

			EditorGUILayout.LabelField("Name (no extension)", EditorStyles.boldLabel);
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_name = EditorGUILayout.TextField(_name);
				if (scope.changed) EditorPrefs.SetString(NamePref, _name);
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Persistent instructions (sent with every request)", EditorStyles.boldLabel);
			_persistentScroll = EditorGUILayout.BeginScrollView(_persistentScroll, GUILayout.MinHeight(60), GUILayout.MaxHeight(140));
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_persistentInstructions = EditorGUILayout.TextArea(_persistentInstructions, GUILayout.ExpandHeight(true));
				if (scope.changed) EditorPrefs.SetString(PersistentInstructionsPref, _persistentInstructions);
			}
			EditorGUILayout.EndScrollView();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Prompt", EditorStyles.boldLabel);
			_promptScroll = EditorGUILayout.BeginScrollView(_promptScroll, GUILayout.MinHeight(60), GUILayout.MaxHeight(140));
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_prompt = EditorGUILayout.TextArea(_prompt, GUILayout.ExpandHeight(true));
				if (scope.changed) EditorPrefs.SetString(PromptPref, _prompt);
			}
			EditorGUILayout.EndScrollView();

			EditorGUILayout.Space();
			using (new EditorGUI.DisabledScope(_isRunning))
			{
				if (GUILayout.Button(_isRunning ? "Generating..." : "Generate Mesh"))
				{
					StartGenerate();
				}
			}
			using (new EditorGUI.DisabledScope(!_isRunning))
			{
				if (GUILayout.Button("Cancel"))
				{
					_cts?.Cancel();
				}
			}

			using (new EditorGUI.DisabledScope(_voxBytes == null || _isRunning))
			{
				if (GUILayout.Button("Save .vox to output folder"))
				{
					SaveVox();
				}
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Goxel text (editable)", EditorStyles.boldLabel);
			_goxelScroll = EditorGUILayout.BeginScrollView(_goxelScroll, GUILayout.MinHeight(160));
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_goxelText = EditorGUILayout.TextArea(_goxelText, GUILayout.ExpandHeight(true));
				if (scope.changed)
				{
					// User edited — convert again so preview / .vox match.
					TryConvertCurrentText(logSuccess: false);
				}
			}
			EditorGUILayout.EndScrollView();

			DrawModelSummary();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
			_logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(120), GUILayout.MaxHeight(200));
			_logStyle ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };
			var logText = _log.ToString();
			var logHeight = Mathf.Max(120f, _logStyle.CalcHeight(new GUIContent(logText), EditorGUIUtility.currentViewWidth - 40f));
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
				GUI.Box(rect, "No mesh yet", EditorStyles.helpBox);
			}
		}

		private void StartGenerate()
		{
			if (string.IsNullOrWhiteSpace(_apiKey))
			{
				Log("ERROR: API key is required.");
				return;
			}
			if (string.IsNullOrWhiteSpace(_prompt))
			{
				Log("ERROR: prompt is required.");
				return;
			}

			_log.Clear();
			_isRunning = true;
			_cts = new CancellationTokenSource();
			RunAsync(_cts.Token);
		}

		private async void RunAsync(CancellationToken ct)
		{
			try
			{
				Log("Requesting Goxel text from Claude...");
				using var client = new AnthropicClient(_apiKey);
				var pipeline = new VoxelPipeline(extraInstructions: _persistentInstructions);
				var goxelText = await pipeline.GenerateGoxelTextAsync(_prompt, client, ct, AppendStreamDelta);
				_goxelText = goxelText;

				Log("Converting to .vox...");
				TryConvertCurrentText(logSuccess: true);
			}
			catch (OperationCanceledException)
			{
				Log("Cancelled.");
			}
			catch (Exception ex)
			{
				Log("Failed: " + ex);
			}
			finally
			{
				_isRunning = false;
				_cts?.Dispose();
				_cts = null;
				Repaint();
			}
		}

		private void TryConvertCurrentText(bool logSuccess)
		{
			if (string.IsNullOrWhiteSpace(_goxelText)) return;

			try
			{
				var pipeline = new VoxelPipeline();
				_voxBytes = pipeline.GoxelTextToVox(_goxelText);

				// Write to a scratch path so Voxel Toolkit can import it and
				// produce a Mesh sub-asset for the preview. The user-facing save
				// happens only on the Save button.
				Directory.CreateDirectory(ScratchFolder);
				var scratchPath = Path.Combine(ScratchFolder, ScratchName + ".vox");
				File.WriteAllBytes(scratchPath, _voxBytes);
				if (logSuccess) Log("Generated in memory (use Save to write).");

				AssetDatabase.Refresh();
				_previewMeshPath = scratchPath;
				ReloadPreviewMesh();
			}
			catch (Exception ex)
			{
				if (logSuccess) Log("Convert failed: " + ex);
			}
		}

		private void SaveVox()
		{
			if (_voxBytes == null) return;

			try
			{
				var voxPath = WriteBytes(_voxBytes, ".vox");
				Log($"Saved {voxPath}");
				AssetDatabase.Refresh();
			}
			catch (Exception ex)
			{
				Log("Save failed: " + ex);
			}
		}

		private void ReloadPreviewMesh()
		{
			if (string.IsNullOrEmpty(_previewMeshPath)) return;
			_previewMesh = AssetDatabase.LoadAssetAtPath<Mesh>(_previewMeshPath);
			DestroyPreviewEditor();
		}

		private void RefreshPreviewEditor()
		{
			// If we have a path but no mesh (Voxel Toolkit import is async), retry.
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

		private GUIStyle? _richLabelStyle;
		private GUIStyle? _logStyle;

		private void DrawModelSummary()
		{
			if (string.IsNullOrWhiteSpace(_goxelText)) return;

			VoxelModel model;
			try
			{
				model = GoxelTextParser.Parse(_goxelText);
			}
			catch (Exception)
			{
				return;
			}

			if (model.Voxels.Count == 0) return;

			_richLabelStyle ??= new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };

			var size = model.Size;
			var sb = new StringBuilder();
			sb.Append("Width ").Append(size.x)
				.Append("  Depth ").Append(size.z)
				.Append("  Height ").Append(size.y)
				.Append("  Voxels ").Append(model.Voxels.Count)
				.Append("  Colours ").Append(model.Palette.Length).Append("  ");

			for (var i = 0; i < model.Palette.Length; i++)
			{
				var c = model.Palette[i];
				var hex = $"{c.r:x2}{c.g:x2}{c.b:x2}";
				sb.Append("<color=#").Append(hex).Append(">■</color>");
				sb.Append("<color=#888888>#").Append(hex).Append("</color> ");
			}

			EditorGUILayout.LabelField(sb.ToString(), _richLabelStyle);
		}

		private string WriteBytes(byte[] bytes, string extension)
		{
			Directory.CreateDirectory(_outputFolder);
			var path = Path.Combine(_outputFolder, _name + extension);
			File.WriteAllBytes(path, bytes);
			return path;
		}

		private void Log(string message)
		{
			_log.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ").AppendLine(message);
			Repaint();
		}

		private void AppendStreamDelta(string delta)
		{
			// Write raw deltas to the log so the user sees Claude's response
			// arrive in real time, no timestamp prefix per chunk.
			_log.Append(delta);
			Repaint();
		}
	}
}
