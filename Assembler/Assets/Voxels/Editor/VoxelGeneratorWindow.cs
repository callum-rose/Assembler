using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Assembler.Anthropic;
using UnityEditor;
using UnityEngine;

namespace Assembler.Voxels.Editor
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
		private string _refinePrompt = string.Empty;
		private byte[]? _voxBytes;

		// Chat history for "Refine (chat)" — running user/assistant turns Claude
		// sees in multi-turn refinement. Reset on Generate and on Load .vox.
		private readonly List<AnthropicMessage> _chatHistory = new();

		// One-step undo of _goxelText. Snapshot before AI calls or .vox loads;
		// cleared after the user pops it. Manual edits are not snapshotted.
		private string? _undoGoxelText;

		// Sidecar project (prompt history, kept persistent on Save).
		private VoxelProject _project = new();

		// Set when a .vox is loaded from disk so Save can default to the same path.
		private string? _loadedVoxPath;

		private readonly StringBuilder _log = new();
		private Vector2 _outerScroll;
		private Vector2 _logScroll;
		private Vector2 _promptScroll;
		private Vector2 _refineScroll;
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

			using (new EditorGUI.DisabledScope(_isRunning))
			{
				if (GUILayout.Button("Load .vox..."))
				{
					LoadVox();
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
			EditorGUILayout.LabelField("Refine prompt (sent with current model)", EditorStyles.boldLabel);
			_refineScroll = EditorGUILayout.BeginScrollView(_refineScroll, GUILayout.MinHeight(50), GUILayout.MaxHeight(120));
			_refinePrompt = EditorGUILayout.TextArea(_refinePrompt, GUILayout.ExpandHeight(true));
			EditorGUILayout.EndScrollView();

			var canRefine = !_isRunning && !string.IsNullOrWhiteSpace(_goxelText) && !string.IsNullOrWhiteSpace(_refinePrompt);
			EditorGUILayout.BeginHorizontal();
			using (new EditorGUI.DisabledScope(!canRefine))
			{
				if (GUILayout.Button("Refine (fresh)"))
				{
					StartRefine(useChatHistory: false);
				}
				if (GUILayout.Button("Refine (chat)"))
				{
					StartRefine(useChatHistory: true);
				}
			}
			EditorGUILayout.EndHorizontal();

			using (new EditorGUI.DisabledScope(_undoGoxelText == null || _isRunning))
			{
				if (GUILayout.Button("Undo last change"))
				{
					UndoLast();
				}
			}

			if (_chatHistory.Count > 0)
			{
				EditorGUILayout.LabelField(
					$"Chat history: {_chatHistory.Count} message(s). 'Refine (chat)' continues; 'Refine (fresh)' ignores.",
					EditorStyles.miniLabel);
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
			RunGenerateAsync(_cts.Token);
		}

		private async void RunGenerateAsync(CancellationToken ct)
		{
			try
			{
				SnapshotUndo();
				Log("Requesting Goxel text from Claude...");
				using var client = new AnthropicClient(_apiKey);
				var pipeline = new VoxelPipeline(extraInstructions: _persistentInstructions);
				var goxelText = await pipeline.GenerateGoxelTextAsync(_prompt, client, ct, AppendStreamDelta);
				_goxelText = goxelText;

				// New generation: reset project + chat history.
				_project = new VoxelProject
				{
					prompt = _prompt,
					persistentInstructions = _persistentInstructions,
				};
				_project.history.Add(VoxelProject.HistoryEntry.Create("generate", _prompt, _goxelText));

				_chatHistory.Clear();
				_chatHistory.Add(new AnthropicMessage("user", _prompt));
				_chatHistory.Add(new AnthropicMessage("assistant", "```goxel\n" + VoxelPipeline.SwapYAndZ(_goxelText) + "\n```"));

				Log("\nConverting to .vox...");
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

		private void StartRefine(bool useChatHistory)
		{
			if (string.IsNullOrWhiteSpace(_apiKey))
			{
				Log("ERROR: API key is required.");
				return;
			}
			if (string.IsNullOrWhiteSpace(_goxelText))
			{
				Log("ERROR: no current model to refine.");
				return;
			}
			if (string.IsNullOrWhiteSpace(_refinePrompt))
			{
				Log("ERROR: refine prompt is required.");
				return;
			}

			_isRunning = true;
			_cts = new CancellationTokenSource();
			RunRefineAsync(useChatHistory, _cts.Token);
		}

		private async void RunRefineAsync(bool useChatHistory, CancellationToken ct)
		{
			var instruction = _refinePrompt;
			try
			{
				SnapshotUndo();
				Log(useChatHistory
					? $"\nRefining (chat, {_chatHistory.Count} prior msgs): {instruction}"
					: $"\nRefining (fresh): {instruction}");
				using var client = new AnthropicClient(_apiKey);
				var pipeline = new VoxelPipeline(extraInstructions: _persistentInstructions);
				var chatRef = useChatHistory ? _chatHistory : null;
				var newGoxel = await pipeline.RefineGoxelTextAsync(
					_goxelText, instruction, chatRef, client, ct, AppendStreamDelta);
				_goxelText = newGoxel;

				_project.history.Add(VoxelProject.HistoryEntry.Create(
					useChatHistory ? "refine-chat" : "refine-fresh",
					instruction,
					_goxelText));

				Log("\nConverting to .vox...");
				TryConvertCurrentText(logSuccess: true);
				_refinePrompt = string.Empty;
			}
			catch (OperationCanceledException)
			{
				Log("Cancelled.");
			}
			catch (Exception ex)
			{
				Log("Refine failed: " + ex);
			}
			finally
			{
				_isRunning = false;
				_cts?.Dispose();
				_cts = null;
				Repaint();
			}
		}

		private void LoadVox()
		{
			var startDir = Directory.Exists(_outputFolder) ? _outputFolder : Application.dataPath;
			var path = EditorUtility.OpenFilePanel("Load .vox", startDir, "vox");
			if (string.IsNullOrEmpty(path)) return;

			try
			{
				var bytes = File.ReadAllBytes(path);
				var model = VoxReader.Read(bytes);
				var text = GoxelTextWriter.Write(model);

				SnapshotUndo();
				_goxelText = text;
				_voxBytes = bytes;
				_loadedVoxPath = path;
				_name = Path.GetFileNameWithoutExtension(path);
				EditorPrefs.SetString(NamePref, _name);

				// Sidecar — restore if present, otherwise seed fresh.
				var sidecarPath = VoxelProject.SidecarPathFor(path);
				if (File.Exists(sidecarPath))
				{
					try
					{
						_project = VoxelProject.Load(sidecarPath);
						if (!string.IsNullOrEmpty(_project.prompt))
						{
							_prompt = _project.prompt;
							EditorPrefs.SetString(PromptPref, _prompt);
						}
						if (!string.IsNullOrEmpty(_project.persistentInstructions))
						{
							_persistentInstructions = _project.persistentInstructions;
							EditorPrefs.SetString(PersistentInstructionsPref, _persistentInstructions);
						}
						Log($"Loaded {path} (+ sidecar, {_project.history.Count} history entries).");
					}
					catch (Exception ex)
					{
						Log("Sidecar load failed, starting fresh: " + ex.Message);
						_project = NewProjectFromLoad();
					}
				}
				else
				{
					_project = NewProjectFromLoad();
					Log($"Loaded {path} (no sidecar; new project).");
				}

				_project.history.Add(VoxelProject.HistoryEntry.Create("load", path, _goxelText));

				// Seed chat history with a synthetic assistant turn carrying the
				// loaded model, so "Refine (chat)" still has context for the first
				// turn even when we load a cold .vox.
				_chatHistory.Clear();
				_chatHistory.Add(new AnthropicMessage("user",
					string.IsNullOrEmpty(_project.prompt) ? "(model loaded from disk)" : _project.prompt));
				_chatHistory.Add(new AnthropicMessage("assistant",
					"```goxel\n" + VoxelPipeline.SwapYAndZ(_goxelText) + "\n```"));

				// Update preview from the loaded bytes — write to scratch, refresh.
				Directory.CreateDirectory(ScratchFolder);
				var scratchPath = Path.Combine(ScratchFolder, ScratchName + ".vox");
				File.WriteAllBytes(scratchPath, _voxBytes);
				AssetDatabase.Refresh();
				_previewMeshPath = scratchPath;
				ReloadPreviewMesh();
			}
			catch (Exception ex)
			{
				Log("Load failed: " + ex);
			}
		}

		private VoxelProject NewProjectFromLoad()
		{
			return new VoxelProject
			{
				prompt = string.Empty,
				persistentInstructions = _persistentInstructions,
			};
		}

		private void UndoLast()
		{
			if (_undoGoxelText == null) return;
			_goxelText = _undoGoxelText;
			_undoGoxelText = null;

			// Drop the latest history entry, and (if it was a chat refine) the
			// matching user+assistant pair from the chat history.
			if (_project.history.Count > 0)
			{
				var last = _project.history[^1];
				_project.history.RemoveAt(_project.history.Count - 1);
				if (last.kind == "refine-chat" && _chatHistory.Count >= 2)
				{
					_chatHistory.RemoveAt(_chatHistory.Count - 1);
					_chatHistory.RemoveAt(_chatHistory.Count - 1);
				}
				else if (last.kind == "generate" || last.kind == "load")
				{
					_chatHistory.Clear();
				}
			}

			TryConvertCurrentText(logSuccess: false);
			Log("Reverted last change.");
		}

		private void SnapshotUndo()
		{
			_undoGoxelText = _goxelText;
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
				_project.prompt = _prompt;
				_project.persistentInstructions = _persistentInstructions;
				var sidecarPath = VoxelProject.SidecarPathFor(voxPath);
				VoxelProject.Save(sidecarPath, _project);
				_loadedVoxPath = voxPath;
				Log($"Saved {voxPath} (+ {Path.GetFileName(sidecarPath)}, {_project.history.Count} history entries).");
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
