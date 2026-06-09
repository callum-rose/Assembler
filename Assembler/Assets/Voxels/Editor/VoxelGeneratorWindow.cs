using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Assembler.Anthropic;
using Assembler.Voxels.Editor.Pipeline;
using Assembler.Voxels.Generation;
using Assembler.Voxels.Pipeline;
using Assembler.Voxels.Scripting;
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
		private const string VoxelCapPref = "Assembler.Voxels.ScriptVoxelCap";
		private const string TimeoutPref = "Assembler.Voxels.ScriptTimeoutSeconds";
		private const string MaxIterationsPref = "Assembler.Voxels.ScriptMaxIterations";
		private const string VisionIterationsPref = "Assembler.Voxels.VisionIterations";
		private const string ReferenceVariationsPref = "Assembler.Voxels.ReferenceVariations";
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

		// Procedural-script settings (configurable safety caps) + last script.
		private int _voxelCap = VoxelScriptLimits.Default.MaxVoxels;
		private float _timeoutSeconds = (float)VoxelScriptLimits.Default.WallClock.TotalSeconds;
		private int _maxIterations = VoxelScriptLimits.Default.MaxToolIterations;

		// Reference-image / vision-feedback settings. The image generator is the
		// only external piece; until a host is wired it's the null provider, so the
		// "with reference image" flow degrades to a plain generate + geometry
		// validate, and vision-refine still works off the model's own renders.
		private int _visionIterations = 2;
		private int _referenceVariations = 1;
		private readonly IImageGenerator _imageGenerator = NullImageGenerator.Instance;
		private List<Texture2D> _referenceThumbs = new();
		private List<Texture2D> _renderedThumbs = new();
		private string? _lastScript;
		private bool _scriptFoldout = true;
		private Vector2 _scriptScroll;

		// Result from the most recent pipeline run. Carries chat history,
		// project, and the in-memory vox bytes that feed Save / preview.
		private VoxelPipelineResult? _lastResult;
		private byte[]? _voxBytes;

		// One-step undo of _goxelText. Snapshot before AI calls or .vox loads;
		// cleared after the user pops it. Manual edits are not snapshotted.
		private string? _undoGoxelText;
		private VoxelPipelineResult? _undoLastResult;

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
			_voxelCap = EditorPrefs.GetInt(VoxelCapPref, VoxelScriptLimits.Default.MaxVoxels);
			_timeoutSeconds = EditorPrefs.GetFloat(TimeoutPref, (float)VoxelScriptLimits.Default.WallClock.TotalSeconds);
			_maxIterations = EditorPrefs.GetInt(MaxIterationsPref, VoxelScriptLimits.Default.MaxToolIterations);
			_visionIterations = EditorPrefs.GetInt(VisionIterationsPref, 2);
			_referenceVariations = EditorPrefs.GetInt(ReferenceVariationsPref, 1);
		}

		private VoxelScriptLimits CurrentLimits() => new()
		{
			MaxVoxels = Mathf.Max(1, _voxelCap),
			WallClock = TimeSpan.FromSeconds(Mathf.Max(0.5f, _timeoutSeconds)),
			MaxToolIterations = Mathf.Max(1, _maxIterations),
		};

		private void OnDisable()
		{
			_cts?.Cancel();
			DestroyPreviewEditor();
			ClearThumbs(_referenceThumbs);
			ClearThumbs(_renderedThumbs);
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
				if (scope.changed)
				{
					EditorPrefs.SetString(ApiKeyPref, _apiKey);
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

			EditorGUILayout.LabelField("Name (no extension)", EditorStyles.boldLabel);
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_name = EditorGUILayout.TextField(_name);
				if (scope.changed)
				{
					EditorPrefs.SetString(NamePref, _name);
				}
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Procedural script limits", EditorStyles.boldLabel);
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_voxelCap = EditorGUILayout.IntField("Voxel cap", _voxelCap);
				_timeoutSeconds = EditorGUILayout.FloatField("Timeout (seconds)", _timeoutSeconds);
				_maxIterations = EditorGUILayout.IntField("Max tool iterations", _maxIterations);
				if (scope.changed)
				{
					EditorPrefs.SetInt(VoxelCapPref, _voxelCap);
					EditorPrefs.SetFloat(TimeoutPref, _timeoutSeconds);
					EditorPrefs.SetInt(MaxIterationsPref, _maxIterations);
				}
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Persistent instructions (sent with every request)", EditorStyles.boldLabel);
			_persistentScroll = EditorGUILayout.BeginScrollView(_persistentScroll, GUILayout.MinHeight(60), GUILayout.MaxHeight(140));
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_persistentInstructions = EditorGUILayout.TextArea(_persistentInstructions, GUILayout.ExpandHeight(true));
				if (scope.changed)
				{
					EditorPrefs.SetString(PersistentInstructionsPref, _persistentInstructions);
				}
			}
			EditorGUILayout.EndScrollView();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Prompt", EditorStyles.boldLabel);
			_promptScroll = EditorGUILayout.BeginScrollView(_promptScroll, GUILayout.MinHeight(60), GUILayout.MaxHeight(140));
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_prompt = EditorGUILayout.TextArea(_prompt, GUILayout.ExpandHeight(true));
				if (scope.changed)
				{
					EditorPrefs.SetString(PromptPref, _prompt);
				}
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
			EditorGUILayout.LabelField("Reference image & vision feedback", EditorStyles.boldLabel);
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_referenceVariations = Mathf.Clamp(EditorGUILayout.IntField("Reference variations", _referenceVariations), 1, 8);
				_visionIterations = Mathf.Clamp(EditorGUILayout.IntField("Vision refine iterations", _visionIterations), 1, 8);
				if (scope.changed)
				{
					EditorPrefs.SetInt(ReferenceVariationsPref, _referenceVariations);
					EditorPrefs.SetInt(VisionIterationsPref, _visionIterations);
				}
			}

			if (_imageGenerator is NullImageGenerator)
			{
				EditorGUILayout.HelpBox(
					"No image provider configured: 'Generate with reference image' runs a plain generate + " +
					"geometry validation, and 'Auto-refine with vision' critiques the model's own renders. " +
					"Wire an IImageGenerator to add a visual anchor.",
					MessageType.Info);
			}

			using (new EditorGUI.DisabledScope(_isRunning))
			{
				if (GUILayout.Button("Generate with reference image"))
				{
					StartReferenceGenerate();
				}
			}

			using (new EditorGUI.DisabledScope(_isRunning || string.IsNullOrWhiteSpace(_goxelText)))
			{
				if (GUILayout.Button("Auto-refine with vision"))
				{
					StartVisionRefine();
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

			var historyCount = _lastResult?.ChatHistory.Count ?? 0;
			if (historyCount > 0)
			{
				EditorGUILayout.LabelField(
					$"Chat history: {historyCount} message(s). 'Refine (chat)' continues; 'Refine (fresh)' ignores.",
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
			DrawGeneratedScript();

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

			DrawThumbnailRow("Reference (what Claude saw)", _referenceThumbs);
			DrawThumbnailRow("Last vision renders", _renderedThumbs);
		}

		private void DrawThumbnailRow(string label, List<Texture2D> thumbs)
		{
			if (thumbs.Count == 0)
			{
				return;
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
			EditorGUILayout.BeginHorizontal();
			const float thumbSize = 72f;
			foreach (var tex in thumbs)
			{
				if (tex == null)
				{
					continue;
				}

				var r = GUILayoutUtility.GetRect(thumbSize, thumbSize, GUILayout.Width(thumbSize), GUILayout.Height(thumbSize));
				GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
			}
			EditorGUILayout.EndHorizontal();
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

				var scratchPath = Path.Combine(ScratchFolder, ScratchName + ".vox");
				var limits = CurrentLimits();
				var result = await VoxelGenerationPipeline
					.CreateNew(EditorVoxelServices.Default)
					.WithAnthropic(client)
					.WithScriptExecutor(new VoxelScriptExecutor(limits))
					.WithScriptLimits(limits)
					.WithPersistentInstructions(_persistentInstructions)
					.WithPrompt(_prompt)
					.WithObserver(new EditorWindowObserver(this))
					.DedupeVoxels()
					.RecordHistory("generate")
					.ParseModel()
					.EncodeVox()
					.WriteScratchPreview(scratchPath)
					.RefreshAssetDatabase()
					.LoadPreviewMesh(scratchPath)
					.ExecuteAsync(ct);

				ApplyResult(result, scratchPath);
				Log("Done.");
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
				var historyCount = _lastResult?.ChatHistory.Count ?? 0;
				Log(useChatHistory
					? $"\nRefining (chat, {historyCount} prior msgs): {instruction}"
					: $"\nRefining (fresh): {instruction}");
				using var client = new AnthropicClient(_apiKey);

				var scratchPath = Path.Combine(ScratchFolder, ScratchName + ".vox");

				// Start from prior result if we have one (carries ChatHistory + Project),
				// otherwise build a fresh context seeded with the current text.
				var pipeline = _lastResult != null
					? VoxelGenerationPipeline.FromExisting(_lastResult, EditorVoxelServices.Default)
					: SeedFromCurrentText(EditorVoxelServices.Default);

				var limits = CurrentLimits();
				var result = await pipeline
					.WithAnthropic(client)
					.WithScriptExecutor(new VoxelScriptExecutor(limits))
					.WithScriptLimits(limits)
					.WithPersistentInstructions(_persistentInstructions)
					.WithObserver(new EditorWindowObserver(this))
					.Refine(instruction, useChatHistory)
					.DedupeVoxels()
					.RecordHistory(useChatHistory ? "refine-chat" : "refine-fresh")
					.ParseModel()
					.EncodeVox()
					.WriteScratchPreview(scratchPath)
					.RefreshAssetDatabase()
					.LoadPreviewMesh(scratchPath)
					.ExecuteAsync(ct);

				ApplyResult(result, scratchPath);
				_refinePrompt = string.Empty;
				Log("Done.");
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

		private void StartReferenceGenerate()
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
			RunReferenceGenerateAsync(_cts.Token);
		}

		private async void RunReferenceGenerateAsync(CancellationToken ct)
		{
			try
			{
				SnapshotUndo();
				Log("Generating reference image + reference-guided model...");
				using var client = new AnthropicClient(_apiKey);

				var scratchPath = Path.Combine(ScratchFolder, ScratchName + ".vox");
				var limits = CurrentLimits();
				var result = await VoxelGenerationPipeline
					.CreateNew(EditorVoxelServices.Default)
					.WithAnthropic(client)
					.WithScriptExecutor(new VoxelScriptExecutor(limits))
					.WithScriptLimits(limits)
					.WithImageGenerator(_imageGenerator)
					.WithReferenceVariations(_referenceVariations)
					.WithPersistentInstructions(_persistentInstructions)
					.WithReferencePrompt(_prompt)
					.WithObserver(new EditorWindowObserver(this))
					.GenerateReferenceImage()
					.ReferenceGuidedGenerate()
					.DedupeVoxels()
					.RecordHistory("generate")
					.ParseModel()
					.ValidateGeometry()
					.EncodeVox()
					.WriteScratchPreview(scratchPath)
					.RefreshAssetDatabase()
					.LoadPreviewMesh(scratchPath)
					.ExecuteAsync(ct);

				ApplyResult(result, scratchPath);
				Log("Done.");
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

		private void StartVisionRefine()
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

			_isRunning = true;
			_cts = new CancellationTokenSource();
			RunVisionRefineAsync(_cts.Token);
		}

		private async void RunVisionRefineAsync(CancellationToken ct)
		{
			try
			{
				SnapshotUndo();
				Log($"\nAuto-refining with vision ({_visionIterations} iteration(s))...");
				using var client = new AnthropicClient(_apiKey);

				var scratchPath = Path.Combine(ScratchFolder, ScratchName + ".vox");

				// Start from the prior result if we have one (carries ReferenceImages
				// so the critique has its original target), else seed from current text.
				var pipeline = _lastResult != null
					? VoxelGenerationPipeline.FromExisting(_lastResult, EditorVoxelServices.Default)
					: SeedFromCurrentText(EditorVoxelServices.Default);

				var limits = CurrentLimits();
				var options = new VisionRefinementOptions { Iterations = Mathf.Max(1, _visionIterations) };
				var result = await pipeline
					.WithAnthropic(client)
					.WithScriptExecutor(new VoxelScriptExecutor(limits))
					.WithScriptLimits(limits)
					.WithPersistentInstructions(_persistentInstructions)
					.WithObserver(new EditorWindowObserver(this))
					.ParseModel()
					.ValidateGeometry()
					.RefineWithVision(options, scratchPath)
					.RecordHistory("refine-chat")
					.EncodeVox()
					.WriteScratchPreview(scratchPath)
					.RefreshAssetDatabase()
					.LoadPreviewMesh(scratchPath)
					.ExecuteAsync(ct);

				ApplyResult(result, scratchPath);
				Log("Done.");
			}
			catch (OperationCanceledException)
			{
				Log("Cancelled.");
			}
			catch (Exception ex)
			{
				Log("Vision refine failed: " + ex);
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
			if (string.IsNullOrEmpty(path))
			{
				return;
			}

			try
			{
				SnapshotUndo();

				var bytes = File.ReadAllBytes(path);
				var model = VoxReader.Read(bytes);
				var text = GoxelTextWriter.Write(model);

				_goxelText = text;
				_voxBytes = bytes;
				_lastScript = null;
				_loadedVoxPath = path;
				_name = Path.GetFileNameWithoutExtension(path);
				EditorPrefs.SetString(NamePref, _name);

				// Sidecar — restore if present, otherwise seed fresh.
				VoxelProject project;
				var sidecarPath = VoxelProject.SidecarPathFor(path);
				if (File.Exists(sidecarPath))
				{
					try
					{
						project = VoxelProject.Load(sidecarPath);
						if (!string.IsNullOrEmpty(project.prompt))
						{
							_prompt = project.prompt;
							EditorPrefs.SetString(PromptPref, _prompt);
						}
						if (!string.IsNullOrEmpty(project.persistentInstructions))
						{
							_persistentInstructions = project.persistentInstructions;
							EditorPrefs.SetString(PersistentInstructionsPref, _persistentInstructions);
						}
						Log($"Loaded {path} (+ sidecar, {project.history.Count} history entries).");
					}
					catch (Exception ex)
					{
						Log("Sidecar load failed, starting fresh: " + ex.Message);
						project = NewProjectFromLoad();
					}
				}
				else
				{
					project = NewProjectFromLoad();
					Log($"Loaded {path} (no sidecar; new project).");
				}

				project.history.Add(new VoxelProject.HistoryEntry
				{
					kind = "load",
					prompt = path,
					goxelText = _goxelText,
					timestampIso = DateTime.UtcNow.ToString("o"),
				});

				// Seed chat history with a synthetic assistant turn carrying the
				// loaded model, so "Refine (chat)" still has context for the first
				// turn even when we load a cold .vox.
				var chat = System.Collections.Immutable.ImmutableList.Create(
					new AnthropicMessage("user",
						string.IsNullOrEmpty(project.prompt) ? "(model loaded from disk)" : project.prompt),
					new AnthropicMessage("assistant",
						"```goxel\n" + GoxelCoordinateConverter.SwapYAndZ(_goxelText) + "\n```"));

				_lastResult = new VoxelPipelineResult(new VoxelPipelineContext
				{
					FileSink = EditorVoxelServices.Default.FileSink,
					AssetDb = EditorVoxelServices.Default.AssetDb,
					Observer = EditorVoxelServices.Default.Observer,
					Clock = EditorVoxelServices.Default.Clock,
					GoxelTextZUp = _goxelText,
					Model = model,
					VoxBytes = bytes,
					ChatHistory = chat,
					Project = project,
					PersistentInstructions = _persistentInstructions,
					UserPrompt = project.prompt,
				});

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
			if (_undoGoxelText == null)
			{
				return;
			}

			_goxelText = _undoGoxelText;
			_lastResult = _undoLastResult;
			_undoGoxelText = null;
			_undoLastResult = null;

			TryConvertCurrentText(logSuccess: false);
			Log("Reverted last change.");
		}

		private void SnapshotUndo()
		{
			_undoGoxelText = _goxelText;
			_undoLastResult = _lastResult;
		}

		private void TryConvertCurrentText(bool logSuccess)
		{
			if (string.IsNullOrWhiteSpace(_goxelText))
			{
				return;
			}

			try
			{
				var scratchPath = Path.Combine(ScratchFolder, ScratchName + ".vox");
				var task = SeedFromCurrentText(EditorVoxelServices.Default)
					.ParseModel()
					.EncodeVox()
					.WriteScratchPreview(scratchPath)
					.RefreshAssetDatabase()
					.LoadPreviewMesh(scratchPath)
					.ExecuteAsync(CancellationToken.None);

				// Synchronous wait is safe here — all stages on this path are sync
				// (no Claude call), and the editor caller is on the main thread.
				task.GetAwaiter().GetResult();
				var result = task.Result;
				ApplyResult(result, scratchPath);
				if (logSuccess)
				{
					Log("Generated in memory (use Save to write).");
				}
			}
			catch (Exception ex)
			{
				if (logSuccess)
				{
					Log("Convert failed: " + ex);
				}
			}
		}

		private void SaveVox()
		{
			if (_voxBytes == null)
			{
				return;
			}

			try
			{
				var voxPath = WriteBytes(_voxBytes, ".vox");

				// Update project metadata and write sidecar.
				var project = _lastResult?.Project ?? new VoxelProject();
				project.prompt = _prompt;
				project.persistentInstructions = _persistentInstructions;
				var sidecarPath = VoxelProject.SidecarPathFor(voxPath);
				VoxelProject.Save(sidecarPath, project);
				_loadedVoxPath = voxPath;
				Log($"Saved {voxPath} (+ {Path.GetFileName(sidecarPath)}, {project.history.Count} history entries).");
				AssetDatabase.Refresh();
			}
			catch (Exception ex)
			{
				Log("Save failed: " + ex);
			}
		}

		private void ApplyResult(VoxelPipelineResult result, string scratchPath)
		{
			_lastResult = result;
			_goxelText = result.GoxelTextZUp ?? _goxelText;
			_voxBytes = result.VoxBytes ?? _voxBytes;
			_lastScript = result.LastScript;
			_previewMeshPath = scratchPath;
			_previewMesh = result.PreviewMesh ?? _previewMesh;
			if (!string.IsNullOrEmpty(_lastScript))
			{
				Log("Model was built procedurally (see Generated script panel).");
			}
			RebuildThumbnails(result);
			DestroyPreviewEditor();
		}

		private void RebuildThumbnails(VoxelPipelineResult result)
		{
			ClearThumbs(_referenceThumbs);
			ClearThumbs(_renderedThumbs);
			_referenceThumbs = DecodeThumbs(result.ReferenceImages);
			_renderedThumbs = DecodeThumbs(result.RenderedImages);
		}

		private static List<Texture2D> DecodeThumbs(IReadOnlyList<byte[]>? images)
		{
			var list = new List<Texture2D>();
			if (images == null)
			{
				return list;
			}

			foreach (var png in images)
			{
				if (png == null || png.Length == 0)
				{
					continue;
				}

				var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false) { hideFlags = HideFlags.HideAndDontSave };
				if (tex.LoadImage(png))
				{
					list.Add(tex);
				}
				else
				{
					DestroyImmediate(tex);
				}
			}

			return list;
		}

		private static void ClearThumbs(List<Texture2D> thumbs)
		{
			foreach (var tex in thumbs)
			{
				if (tex != null)
				{
					DestroyImmediate(tex);
				}
			}

			thumbs.Clear();
		}

		private VoxelGenerationPipeline SeedFromCurrentText(VoxelPipelineServices services)
		{
			// Build a pipeline whose starting context already has the current
			// goxel text + project + chat history, so refines and re-converts
			// have full state without needing a prior pipeline run.
			var chat = _lastResult?.ChatHistory.Count > 0
				? System.Collections.Immutable.ImmutableList.CreateRange(_lastResult.ChatHistory)
				: System.Collections.Immutable.ImmutableList<AnthropicMessage>.Empty;
			var project = _lastResult?.Project ?? new VoxelProject
			{
				prompt = _prompt,
				persistentInstructions = _persistentInstructions,
			};

			var seedResult = new VoxelPipelineResult(new VoxelPipelineContext
			{
				FileSink = services.FileSink,
				AssetDb = services.AssetDb,
				Observer = services.Observer,
				Clock = services.Clock,
				GoxelTextZUp = _goxelText,
				ChatHistory = chat,
				Project = project,
				PersistentInstructions = _persistentInstructions,
				UserPrompt = _prompt,
			});
			return VoxelGenerationPipeline.FromExisting(seedResult, services);
		}

		private void ReloadPreviewMesh()
		{
			if (string.IsNullOrEmpty(_previewMeshPath))
			{
				return;
			}

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
			if (string.IsNullOrWhiteSpace(_goxelText))
			{
				return;
			}

			VoxelModel model;
			try
			{
				model = GoxelTextParser.Parse(_goxelText);
			}
			catch (Exception)
			{
				return;
			}

			if (model.Voxels.Count == 0)
			{
				return;
			}

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

		private void DrawGeneratedScript()
		{
			if (string.IsNullOrEmpty(_lastScript))
			{
				return;
			}

			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			_scriptFoldout = EditorGUILayout.Foldout(_scriptFoldout, "Generated script (read-only)", true);
			if (GUILayout.Button("Copy", GUILayout.Width(60)))
			{
				EditorGUIUtility.systemCopyBuffer = _lastScript;
				Log("Copied script to clipboard.");
			}
			EditorGUILayout.EndHorizontal();

			if (_scriptFoldout)
			{
				_scriptScroll = EditorGUILayout.BeginScrollView(_scriptScroll, GUILayout.MinHeight(80), GUILayout.MaxHeight(220));
				using (new EditorGUI.DisabledScope(true))
				{
					EditorGUILayout.TextArea(_lastScript, GUILayout.ExpandHeight(true));
				}
				EditorGUILayout.EndScrollView();
			}
		}

		private string WriteBytes(byte[] bytes, string extension)
		{
			Directory.CreateDirectory(_outputFolder);
			var path = Path.Combine(_outputFolder, _name + extension);
			File.WriteAllBytes(path, bytes);
			return path;
		}

		internal void Log(string message)
		{
			_log.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ").AppendLine(message);
			Repaint();
		}

		internal void AppendStreamDelta(string delta)
		{
			// Write raw deltas to the log so the user sees Claude's response
			// arrive in real time, no timestamp prefix per chunk.
			_log.Append(delta);
			Repaint();
		}
	}

	/// <summary>
	/// Wires pipeline observer events into the window's log + repaint loop.
	/// Marshals through <see cref="EditorMainThreadDispatcher"/> so callbacks
	/// fired from the Anthropic SDK's streaming continuation (which lands on a
	/// thread pool thread) are queued back to the Unity main thread before
	/// touching the window.
	/// </summary>
	internal sealed class EditorWindowObserver : IVoxelPipelineObserver
	{
		private readonly VoxelGeneratorWindow _window;
		private readonly IMainThreadDispatcher _dispatcher;

		public EditorWindowObserver(VoxelGeneratorWindow window)
		{
			_window = window;
			_dispatcher = EditorMainThreadDispatcher.Instance;
		}

		public void OnStageStarted(string stageName) => Post(() => _window.Log("→ " + stageName));
		public void OnStageFinished(string stageName, TimeSpan elapsed) { }
		public void OnStageFailed(string stageName, Exception ex) => Post(() => _window.Log($"✗ {stageName}: {ex.Message}"));
		public void OnLog(string message) => Post(() => _window.Log(message));
		public void OnStreamDelta(string delta) => Post(() => _window.AppendStreamDelta(delta));

		private void Post(Action action) => _dispatcher.RunAsync(action);
	}
}
