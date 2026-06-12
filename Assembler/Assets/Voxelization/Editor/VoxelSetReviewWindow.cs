using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Voxels.Scripting;
using UnityEditor;
using UnityEngine;

namespace Assembler.Voxelization.Editor
{
	/// <summary>
	/// Stage 6 — the operator's review gallery. Paste/generate a manifest, run
	/// the batch (assets generate in parallel and auto-export as they finish),
	/// then review rendered previews per model with regenerate / refine. The
	/// right sidebar carries the live status line, elapsed time, per-stage
	/// token usage with estimated spend, and the log.
	/// </summary>
	public sealed class VoxelSetReviewWindow : EditorWindow
	{
		private const string ApiKeyPref = "Assembler.Generation.ApiKey";
		private const string ManifestPref = "Assembler.Voxelization.Manifest";
		private const string BriefPref = "Assembler.Voxelization.GameBrief";
		private const string OutputFolderPref = "Assembler.Voxelization.OutputFolder";
		private const string ImageFolderPref = "Assembler.Voxelization.ImageFolder";
		private const string StylePref = "Assembler.Voxelization.StyleGuidance";
		private const string StageModelPrefPrefix = "Assembler.Voxelization.Model.";
		private const float PreviewSize = 200f;
		private const float SidebarWidth = 360f;

		private static readonly string[] ModelOptions =
		{
			"claude-sonnet-4-6",
			"claude-haiku-4-5",
			"claude-opus-4-8",
		};

		private string _apiKey = string.Empty;
		private string _gameBrief = string.Empty;
		private string _manifestYaml = string.Empty;
		private string _outputFolder = "Assets/Resources/Voxels/Sets/";
		private string _imageFolder = string.Empty;
		private string _styleGuidance = string.Empty;
		private string _manifestModel = VoxelizationConfig.DefaultModel;
		private string _planningModel = VoxelizationConfig.DefaultModel;
		private string _authoringModel = VoxelizationConfig.DefaultModel;

		private readonly Dictionary<string, ModelResult> _results = new();
		private readonly Dictionary<string, Texture2D> _previews = new();
		private readonly Dictionary<string, string> _refineNotes = new();
		private readonly Dictionary<string, Vector2> _infoScrolls = new();
		private readonly Dictionary<string, string> _inFlight = new();
		private readonly StringBuilder _log = new();
		private TokenUsageTracker _usage = new();

		private readonly System.Diagnostics.Stopwatch _runTimer = new();
		private string _statusLine = string.Empty;
		private string _runFolder = string.Empty;
		private bool _isRunning;
		private CancellationTokenSource? _cts;
		private Vector2 _scroll;
		private Vector2 _briefScroll;
		private Vector2 _styleScroll;
		private Vector2 _manifestScroll;
		private Vector2 _logScroll;
		private GUIStyle? _logStyle;
		private GUIStyle? _statusStyle;
		private GUIStyle? _infoStyle;
		private GUIStyle? _wrappedTextArea;

		[MenuItem("Assembler/Voxel Set Review")]
		public static void Open()
		{
			var window = GetWindow<VoxelSetReviewWindow>("Voxel Set Review");
			window.minSize = new Vector2(960, 600);
			window.Show();
		}

		private void OnEnable()
		{
			_apiKey = EditorPrefs.GetString(ApiKeyPref, string.Empty);
			_gameBrief = EditorPrefs.GetString(BriefPref, string.Empty);
			_manifestYaml = EditorPrefs.GetString(ManifestPref, string.Empty);
			_outputFolder = EditorPrefs.GetString(OutputFolderPref, _outputFolder);
			_imageFolder = EditorPrefs.GetString(ImageFolderPref, string.Empty);
			_styleGuidance = EditorPrefs.GetString(StylePref, string.Empty);
			_manifestModel = EditorPrefs.GetString(StageModelPrefPrefix + "Manifest", VoxelizationConfig.DefaultModel);
			_planningModel = EditorPrefs.GetString(StageModelPrefPrefix + "Planning", VoxelizationConfig.DefaultModel);
			_authoringModel = EditorPrefs.GetString(StageModelPrefPrefix + "Authoring", VoxelizationConfig.DefaultModel);
			EditorApplication.update += OnEditorUpdate;
		}

		private void OnDisable()
		{
			EditorApplication.update -= OnEditorUpdate;
			_cts?.Cancel();
		}

		private void OnGUI()
		{
			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.BeginVertical();

			// Vertical-only: a no-wrap text line must never widen the panel.
			_scroll = EditorGUILayout.BeginScrollView(
				_scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUI.skin.scrollView);
			DrawSettings();
			EditorGUILayout.Space();
			DrawManifest();
			EditorGUILayout.Space();
			DrawRunControls();
			EditorGUILayout.Space();
			DrawGallery();
			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();

			EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(SidebarWidth));
			DrawSidebar();
			EditorGUILayout.EndVertical();

			EditorGUILayout.EndHorizontal();
		}

		private void OnEditorUpdate()
		{
			// Keep the timer and streaming status line moving while a run is live.
			if (_isRunning)
			{
				Repaint();
			}
		}

		private VoxelizationConfig BuildConfig() => VoxelizationConfig.Default with
		{
			ManifestModel = _manifestModel,
			PlanningModel = _planningModel,
			AuthoringModel = _authoringModel,
			StyleGuidance = _styleGuidance.Trim(),
		};

		private void DrawSettings()
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

			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_outputFolder = EditorGUILayout.TextField("Output folder", _outputFolder);
				if (scope.changed)
				{
					EditorPrefs.SetString(OutputFolderPref, _outputFolder);
				}
			}

			EditorGUILayout.BeginHorizontal();
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_imageFolder = EditorGUILayout.TextField("Reference image folder", _imageFolder);
				if (scope.changed)
				{
					EditorPrefs.SetString(ImageFolderPref, _imageFolder);
				}
			}

			if (GUILayout.Button(EditorGUIUtility.IconContent("Folder Icon"), GUILayout.Width(30), GUILayout.Height(18)))
			{
				var picked = EditorUtility.OpenFolderPanel("Reference image folder", _imageFolder, string.Empty);
				if (!string.IsNullOrEmpty(picked))
				{
					_imageFolder = picked;
					EditorPrefs.SetString(ImageFolderPref, _imageFolder);
				}
			}

			EditorGUILayout.EndHorizontal();

			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_manifestModel = DrawModelPopup("Manifest model", _manifestModel);
				_planningModel = DrawModelPopup("Planning model", _planningModel);
				_authoringModel = DrawModelPopup("Authoring model", _authoringModel);
				if (scope.changed)
				{
					EditorPrefs.SetString(StageModelPrefPrefix + "Manifest", _manifestModel);
					EditorPrefs.SetString(StageModelPrefPrefix + "Planning", _planningModel);
					EditorPrefs.SetString(StageModelPrefPrefix + "Authoring", _authoringModel);
				}
			}
		}

		private static string DrawModelPopup(string label, string current)
		{
			var index = Mathf.Max(0, Array.IndexOf(ModelOptions, current));
			return ModelOptions[EditorGUILayout.Popup(label, index, ModelOptions)];
		}

		private void DrawManifest()
		{
			// EditorStyles.textArea does not word-wrap, so long prose becomes one
			// wide line: it never overflows vertically (no scrollbar) and pushes
			// the panel wide instead. Wrapping fixes both — the layout system
			// computes the wrapped height, so each box's scroll view scrolls.
			_wrappedTextArea ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };

			EditorGUILayout.LabelField("Game brief (Stage 0 input)", EditorStyles.boldLabel);
			_briefScroll = EditorGUILayout.BeginScrollView(_briefScroll, GUILayout.MinHeight(40), GUILayout.MaxHeight(120));
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_gameBrief = EditorGUILayout.TextArea(_gameBrief, _wrappedTextArea, GUILayout.ExpandHeight(true));
				if (scope.changed)
				{
					EditorPrefs.SetString(BriefPref, _gameBrief);
				}
			}

			EditorGUILayout.EndScrollView();

			using (new EditorGUI.DisabledScope(_isRunning || string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_gameBrief)))
			{
				if (GUILayout.Button("Generate manifest from brief"))
				{
					RunGenerateManifestAsync();
				}
			}

			EditorGUILayout.LabelField("Style guidance (applies to every asset in every run)", EditorStyles.boldLabel);
			_styleScroll = EditorGUILayout.BeginScrollView(_styleScroll, GUILayout.MinHeight(36), GUILayout.MaxHeight(100));
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_styleGuidance = EditorGUILayout.TextArea(_styleGuidance, _wrappedTextArea, GUILayout.ExpandHeight(true));
				if (scope.changed)
				{
					EditorPrefs.SetString(StylePref, _styleGuidance);
				}
			}

			EditorGUILayout.EndScrollView();

			EditorGUILayout.LabelField("Manifest yaml (editable — the scale bible)", EditorStyles.boldLabel);
			_manifestScroll = EditorGUILayout.BeginScrollView(_manifestScroll, GUILayout.MinHeight(80), GUILayout.MaxHeight(240));
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_manifestYaml = EditorGUILayout.TextArea(_manifestYaml, _wrappedTextArea, GUILayout.ExpandHeight(true));
				if (scope.changed)
				{
					EditorPrefs.SetString(ManifestPref, _manifestYaml);
				}
			}

			EditorGUILayout.EndScrollView();
		}

		private void DrawRunControls()
		{
			EditorGUILayout.BeginHorizontal();
			using (new EditorGUI.DisabledScope(_isRunning || string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_manifestYaml)))
			{
				if (GUILayout.Button(_isRunning ? "Running..." : "Run batch (assets in parallel)"))
				{
					RunBatchAsync();
				}
			}

			using (new EditorGUI.DisabledScope(!_isRunning))
			{
				if (GUILayout.Button("Cancel", GUILayout.Width(80)))
				{
					_cts?.Cancel();
				}
			}

			EditorGUILayout.EndHorizontal();
		}

		private void DrawGallery()
		{
			if (_results.Count == 0 && _inFlight.Count == 0)
			{
				return;
			}

			EditorGUILayout.LabelField("Gallery (models auto-export to this run's subfolder as they finish)", EditorStyles.boldLabel);
			foreach (var result in _results.Values.ToList())
			{
				DrawResult(result);
				EditorGUILayout.Space();
			}

			// Assets still generating get a placeholder box from the moment the
			// run starts, showing the latest progress line for that asset.
			foreach (var pending in _inFlight.Where(kv => !_results.ContainsKey(kv.Key)).ToList())
			{
				DrawPlaceholder(pending.Key, pending.Value);
				EditorGUILayout.Space();
			}
		}

		private void DrawPlaceholder(string assetId, string activity)
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.BeginHorizontal();

			var previewRect = GUILayoutUtility.GetRect(
				PreviewSize, PreviewSize, GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));
			GUI.Box(previewRect, $"generating{Dots()}");

			EditorGUILayout.BeginVertical();
			EditorGUILayout.LabelField($"{assetId} — processing", EditorStyles.boldLabel);
			_statusStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
			EditorGUILayout.LabelField(activity, _statusStyle);
			EditorGUILayout.EndVertical();

			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndVertical();
		}

		private static string Dots() => new('.', 1 + (int)(EditorApplication.timeSinceStartup * 2) % 3);

		private void DrawResult(ModelResult result)
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.BeginHorizontal();

			var previewRect = GUILayoutUtility.GetRect(
				PreviewSize, PreviewSize, GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));
			if (PreviewFor(result) is { } preview)
			{
				GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
			}
			else
			{
				GUI.Box(previewRect, "no preview");
			}

			EditorGUILayout.BeginVertical();
			EditorGUILayout.LabelField($"{result.AssetId} — {result.Status}", EditorStyles.boldLabel);
			if (result.Assembled is { } assembled && assembled.Composed.Voxels.Count > 0)
			{
				var size = assembled.Composed.Size;
				EditorGUILayout.LabelField(
					$"{size.x} wide x {size.y} tall x {size.z} long — {assembled.Composed.Voxels.Count:n0} voxels — " +
					$"{result.Model.Parts.Count} parts",
					EditorStyles.miniLabel);
			}

			if (_inFlight.TryGetValue(result.AssetId, out var activity))
			{
				_statusStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
				EditorGUILayout.LabelField($"regenerating{Dots()} {activity}", _statusStyle);
			}

			DrawResultInfo(result);

			EditorGUILayout.BeginHorizontal();
			using (new EditorGUI.DisabledScope(_isRunning || result.Export == null))
			{
				if (GUILayout.Button("Re-export", GUILayout.Width(90)))
				{
					ExportToAssets(result);
				}
			}

			using (new EditorGUI.DisabledScope(_isRunning))
			{
				if (GUILayout.Button("Regenerate", GUILayout.Width(90)))
				{
					RunSingleAssetAsync(result.AssetId, string.Empty);
				}
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			_refineNotes.TryGetValue(result.AssetId, out var note);
			note = EditorGUILayout.TextField(note ?? string.Empty);
			_refineNotes[result.AssetId] = note;
			using (new EditorGUI.DisabledScope(_isRunning || string.IsNullOrWhiteSpace(note)))
			{
				if (GUILayout.Button("Refine", GUILayout.Width(90)))
				{
					RunRefineAsync(result.AssetId, note);
				}
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndVertical();
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndVertical();
		}

		private void DrawResultInfo(ModelResult result)
		{
			var text = ResultInfoText(result);
			_infoStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = false };

			// One selectable label sized to its content: the surrounding scroll
			// view scrolls both axes and the text is copyable like the log.
			var size = _infoStyle.CalcSize(new GUIContent(text));
			_infoScrolls.TryGetValue(result.AssetId, out var scroll);
			scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(40), GUILayout.MaxHeight(PreviewSize - 60));
			_infoScrolls[result.AssetId] = scroll;
			EditorGUILayout.SelectableLabel(
				text, _infoStyle, GUILayout.MinWidth(size.x + 10), GUILayout.MinHeight(size.y + 4));
			EditorGUILayout.EndScrollView();
		}

		private static string ResultInfoText(ModelResult result)
		{
			var lines = new List<string>();
			if (result.Error.Length > 0)
			{
				lines.Add("FAILED: " + result.Error);
			}

			lines.AddRange(result.Report.Issues.Select(i => i.ToString()));
			if (result.Error.Length == 0 && result.Report.IsValid)
			{
				lines.Add("validation clean");
			}

			return string.Join("\n", lines);
		}

		private void DrawSidebar()
		{
			var elapsed = _runTimer.Elapsed;
			EditorGUILayout.LabelField(
				_isRunning ? $"Running — {elapsed:mm\\:ss}" : elapsed.Ticks > 0 ? $"Done in {elapsed:mm\\:ss}" : "Idle",
				EditorStyles.boldLabel);

			if (_isRunning && _statusLine.Length > 0)
			{
				_statusStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
				EditorGUILayout.LabelField(_statusLine, _statusStyle);
			}

			DrawUsage();
			EditorGUILayout.Space();
			DrawLog();
		}

		private void DrawUsage()
		{
			var snapshot = _usage.Snapshot();
			if (snapshot.Count == 0)
			{
				return;
			}

			var config = BuildConfig();
			var totalUsd = 0.0;
			foreach (var stage in snapshot)
			{
				var rates = TokenPricing.RatesFor(config.ModelForStage(stage.Stage));
				var stageUsd = TokenPricing.EstimateUsd(stage.Tokens, rates);
				totalUsd += stageUsd;
				EditorGUILayout.LabelField(
					$"{stage.Stage}: {stage.Requests} req, in {stage.Tokens.InputTokens:n0} " +
					$"(c {stage.Tokens.CacheReadInputTokens:n0}/{stage.Tokens.CacheCreationInputTokens:n0}), " +
					$"out {stage.Tokens.OutputTokens:n0} — ~${stageUsd:0.000}",
					EditorStyles.miniLabel);
			}

			EditorGUILayout.LabelField($"Estimated spend: ~${totalUsd:0.000}", EditorStyles.boldLabel);
		}

		private void DrawLog()
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
			using (new EditorGUI.DisabledScope(_log.Length == 0))
			{
				if (GUILayout.Button("Clear", GUILayout.Width(50)))
				{
					_log.Clear();
				}
			}

			EditorGUILayout.EndHorizontal();
			if (_log.Length == 0)
			{
				return;
			}

			_logStyle ??= new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = 10 };
			var text = _log.ToString();
			var height = _logStyle.CalcHeight(new GUIContent(text), SidebarWidth - 40f);

			_logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.ExpandHeight(true));
			EditorGUILayout.SelectableLabel(text, _logStyle, GUILayout.Height(Mathf.Max(height, 60f)), GUILayout.ExpandWidth(true));
			EditorGUILayout.EndScrollView();
		}

		private Texture2D? PreviewFor(ModelResult result)
		{
			if (_previews.TryGetValue(result.AssetId, out var cached) && cached != null)
			{
				return cached;
			}

			if (result.Export == null || !result.Export.Files.TryGetValue("preview_iso.png", out var png))
			{
				return null;
			}

			var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
			texture.LoadImage(png);
			_previews[result.AssetId] = texture;
			return texture;
		}

		private void ExportToAssets(ModelResult result)
		{
			if (result.Export == null)
			{
				return;
			}

			// Models land in this run's subfolder so successive runs never collide.
			var root = _runFolder.Length > 0 ? _runFolder : _outputFolder;
			var directory = Path.Combine(root, result.AssetId);
			result.Export.WriteToDisk(directory);
			AssetDatabase.Refresh();
			Log($"{result.AssetId}: exported -> {directory}");
		}

		private async void RunGenerateManifestAsync()
		{
			StartRun(clearResults: false, newRunFolder: false);
			try
			{
				using var gateway = NewGateway();
				var generator = new ManifestGenerator(gateway, BuildConfig());
				Log("Generating manifest...");
				var manifest = await generator.GenerateAsync(_gameBrief, _cts!.Token);
				_manifestYaml = ManifestYaml.Write(manifest);
				EditorPrefs.SetString(ManifestPref, _manifestYaml);
				Log("Manifest generated. Review it (attach 'reference:' entries if you have images), then run the batch.");
			}
			catch (OperationCanceledException)
			{
				Log("Cancelled.");
			}
			catch (Exception ex)
			{
				Log("Manifest generation failed: " + ex.Message);
			}
			finally
			{
				FinishRun();
			}
		}

		private async void RunBatchAsync()
		{
			if (!TryParseManifest(out var manifest))
			{
				return;
			}

			StartRun(clearResults: true);
			foreach (var asset in manifest.Assets)
			{
				_inFlight[asset.Id] = "queued...";
			}

			try
			{
				using var gateway = NewGateway();
				var orchestrator = NewOrchestrator(gateway);
				var progress = NewProgress();

				// All assets run concurrently; each stores and auto-exports the
				// moment it completes so finished work survives a recompile.
				await Task.WhenAll(manifest.Assets.Select(async asset =>
				{
					var result = await orchestrator.RunAssetAsync(manifest, asset, string.Empty, _cts!.Token, progress);
					StoreResult(result);
				}));

				Log("Batch complete.");
			}
			catch (OperationCanceledException)
			{
				Log("Cancelled.");
			}
			catch (Exception ex)
			{
				Log("Batch failed: " + ex.Message);
			}
			finally
			{
				FinishRun();
			}
		}

		private async void RunSingleAssetAsync(string assetId, string refinementNote)
		{
			if (!TryParseManifest(out var manifest))
			{
				return;
			}

			var asset = manifest.Assets.FirstOrDefault(a => a.Id == assetId);
			if (asset == null)
			{
				Log($"{assetId}: not present in the current manifest.");
				return;
			}

			StartRun(clearResults: false);
			_inFlight[assetId] = "queued...";
			try
			{
				using var gateway = NewGateway();
				var orchestrator = NewOrchestrator(gateway);
				var result = await orchestrator.RunAssetAsync(manifest, asset, refinementNote, _cts!.Token, NewProgress());
				StoreResult(result);
			}
			catch (OperationCanceledException)
			{
				Log("Cancelled.");
			}
			catch (Exception ex)
			{
				Log($"{assetId} failed: " + ex.Message);
			}
			finally
			{
				FinishRun();
			}
		}

		/// <summary>
		/// The lightweight refine path: edit the already-generated model in place
		/// from the note, touching only the parts it names. Falls back to a full
		/// regenerate-with-note when there is no prior good model to edit.
		/// </summary>
		private async void RunRefineAsync(string assetId, string note)
		{
			if (!_results.TryGetValue(assetId, out var previous) || previous.Status == ModelStatus.Failed
				|| previous.Model.Parts.Count == 0)
			{
				RunSingleAssetAsync(assetId, note);
				return;
			}

			StartRun(clearResults: false);
			_inFlight[assetId] = "queued...";
			try
			{
				using var gateway = NewGateway();
				var orchestrator = NewOrchestrator(gateway);
				var result = await orchestrator.RefineAssetAsync(previous, note, _cts!.Token, NewProgress());
				StoreResult(result);
			}
			catch (OperationCanceledException)
			{
				Log("Cancelled.");
			}
			catch (Exception ex)
			{
				Log($"{assetId} refine failed: " + ex.Message);
			}
			finally
			{
				FinishRun();
			}
		}

		private AnthropicGateway NewGateway() =>
			new(_apiKey, _usage, onActivity: status => _statusLine = status);

		private SetOrchestrator NewOrchestrator(AnthropicGateway gateway)
		{
			var config = BuildConfig();
			var images = string.IsNullOrWhiteSpace(_imageFolder)
				? (IReferenceImageSource)NullReferenceImageSource.Instance
				: new FileReferenceImageSource(_imageFolder);
			var runner = new ExecutorPartScriptRunner(new VoxelScriptExecutor(config.ScriptLimits));
			return new SetOrchestrator(gateway, config, images, runner, _usage);
		}

		private bool TryParseManifest(out SetManifest manifest)
		{
			try
			{
				manifest = ManifestYaml.Read(_manifestYaml);
				return true;
			}
			catch (Exception ex)
			{
				Log("Manifest yaml is invalid: " + ex.Message);
				manifest = new SetManifest();
				return false;
			}
		}

		private IProgress<string> NewProgress() => new Progress<string>(message =>
		{
			Log(message);
			UpdateActivity(message);
			Repaint();
		});

		/// <summary>
		/// Progress lines are "assetId: what's happening"; the gallery placeholder
		/// for that asset shows the latest one.
		/// </summary>
		private void UpdateActivity(string message)
		{
			var firstLine = message.Split('\n')[0];
			var split = firstLine.IndexOf(": ", StringComparison.Ordinal);
			if (split <= 0)
			{
				return;
			}

			var assetId = firstLine[..split];
			if (_inFlight.ContainsKey(assetId))
			{
				_inFlight[assetId] = firstLine[(split + 2)..];
			}
		}

		private void StoreResult(ModelResult result)
		{
			_inFlight.Remove(result.AssetId);
			_results[result.AssetId] = result;
			if (_previews.TryGetValue(result.AssetId, out var old) && old != null)
			{
				DestroyImmediate(old);
			}

			_previews.Remove(result.AssetId);
			Log($"{result.AssetId}: {result.Status}");

			// Auto-export so the model is inspectable in the project (and on
			// disk, surviving a domain reload) without waiting for the batch.
			if (result.Export != null)
			{
				ExportToAssets(result);
			}
		}

		private void StartRun(bool clearResults, bool newRunFolder = true)
		{
			_isRunning = true;
			_cts = new CancellationTokenSource();
			_statusLine = string.Empty;
			_log.Clear();
			_inFlight.Clear();
			_runFolder = newRunFolder
				? Path.Combine(_outputFolder, $"run-{DateTime.Now:yyyy-MM-dd-HHmmss}")
				: string.Empty;
			_runTimer.Restart();
			if (clearResults)
			{
				_results.Clear();
				ClearPreviews();
				_usage = new TokenUsageTracker();
			}
		}

		private void ClearPreviews()
		{
			foreach (var texture in _previews.Values.Where(t => t != null))
			{
				DestroyImmediate(texture);
			}

			_previews.Clear();
		}

		private void FinishRun()
		{
			_isRunning = false;
			_runTimer.Stop();
			_statusLine = string.Empty;
			_inFlight.Clear();
			ExportSessionLog();
			_cts?.Dispose();
			_cts = null;
			Repaint();
		}

		private void ExportSessionLog()
		{
			if (_runFolder.Length == 0 || _log.Length == 0)
			{
				return;
			}

			Directory.CreateDirectory(_runFolder);
			File.WriteAllText(Path.Combine(_runFolder, "session.log"), _log.ToString());
			AssetDatabase.Refresh();
		}

		private void Log(string message)
		{
			// Each entry opens with a bullet + run timestamp so entries are easy
			// to tell apart in the stream.
			_log.Append("• ");
			if (_runTimer.IsRunning)
			{
				_log.Append('[').Append(_runTimer.Elapsed.ToString(@"mm\:ss")).Append("] ");
			}

			_log.AppendLine(message);
			_logScroll.y = float.MaxValue;
			Repaint();
		}
	}
}
