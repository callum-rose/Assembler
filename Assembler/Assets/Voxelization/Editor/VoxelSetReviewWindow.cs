using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Assembler.Voxels.Scripting;
using UnityEditor;
using UnityEngine;

namespace Assembler.Voxelization.Editor
{
	/// <summary>
	/// Stage 6 — the operator's review gallery. Paste/generate a manifest, run
	/// the batch autonomously, then review rendered previews per model with
	/// accept (write exports to the output folder), regenerate (re-run from
	/// planning), or refine (re-run with a note). Token usage per stage is
	/// shown from day one.
	/// </summary>
	public sealed class VoxelSetReviewWindow : EditorWindow
	{
		private const string ApiKeyPref = "Assembler.Generation.ApiKey";
		private const string ManifestPref = "Assembler.Voxelization.Manifest";
		private const string BriefPref = "Assembler.Voxelization.GameBrief";
		private const string OutputFolderPref = "Assembler.Voxelization.OutputFolder";
		private const string ImageFolderPref = "Assembler.Voxelization.ImageFolder";
		private const float PreviewSize = 160f;

		private string _apiKey = string.Empty;
		private string _gameBrief = string.Empty;
		private string _manifestYaml = string.Empty;
		private string _outputFolder = "Assets/Resources/Voxels/Sets/";
		private string _imageFolder = string.Empty;

		private readonly Dictionary<string, ModelResult> _results = new();
		private readonly Dictionary<string, Texture2D> _previews = new();
		private readonly Dictionary<string, string> _refineNotes = new();
		private readonly StringBuilder _log = new();
		private TokenUsageTracker _usage = new();

		private bool _isRunning;
		private CancellationTokenSource? _cts;
		private Vector2 _scroll;
		private Vector2 _logScroll;

		[MenuItem("Assembler/Voxel Set Review")]
		public static void Open()
		{
			var window = GetWindow<VoxelSetReviewWindow>("Voxel Set Review");
			window.minSize = new Vector2(820, 600);
			window.Show();
		}

		private void OnEnable()
		{
			_apiKey = EditorPrefs.GetString(ApiKeyPref, string.Empty);
			_gameBrief = EditorPrefs.GetString(BriefPref, string.Empty);
			_manifestYaml = EditorPrefs.GetString(ManifestPref, string.Empty);
			_outputFolder = EditorPrefs.GetString(OutputFolderPref, _outputFolder);
			_imageFolder = EditorPrefs.GetString(ImageFolderPref, string.Empty);
		}

		private void OnDisable()
		{
			_cts?.Cancel();
		}

		private void OnGUI()
		{
			_scroll = EditorGUILayout.BeginScrollView(_scroll);

			DrawSettings();
			EditorGUILayout.Space();
			DrawManifest();
			EditorGUILayout.Space();
			DrawRunControls();
			EditorGUILayout.Space();
			DrawGallery();
			EditorGUILayout.Space();
			DrawUsage();
			DrawLog();

			EditorGUILayout.EndScrollView();
		}

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
				_imageFolder = EditorGUILayout.TextField("Reference image folder", _imageFolder);
				if (scope.changed)
				{
					EditorPrefs.SetString(OutputFolderPref, _outputFolder);
					EditorPrefs.SetString(ImageFolderPref, _imageFolder);
				}
			}
		}

		private void DrawManifest()
		{
			EditorGUILayout.LabelField("Game brief (Stage 0 input)", EditorStyles.boldLabel);
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_gameBrief = EditorGUILayout.TextArea(_gameBrief, GUILayout.MinHeight(40));
				if (scope.changed)
				{
					EditorPrefs.SetString(BriefPref, _gameBrief);
				}
			}

			using (new EditorGUI.DisabledScope(_isRunning || string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_gameBrief)))
			{
				if (GUILayout.Button("Generate manifest from brief"))
				{
					RunGenerateManifestAsync();
				}
			}

			EditorGUILayout.LabelField("Manifest yaml (editable — the scale bible)", EditorStyles.boldLabel);
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_manifestYaml = EditorGUILayout.TextArea(_manifestYaml, GUILayout.MinHeight(80));
				if (scope.changed)
				{
					EditorPrefs.SetString(ManifestPref, _manifestYaml);
				}
			}
		}

		private void DrawRunControls()
		{
			EditorGUILayout.BeginHorizontal();
			using (new EditorGUI.DisabledScope(_isRunning || string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_manifestYaml)))
			{
				if (GUILayout.Button(_isRunning ? "Running..." : "Run batch"))
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
			if (_results.Count == 0)
			{
				return;
			}

			EditorGUILayout.LabelField("Gallery", EditorStyles.boldLabel);
			foreach (var result in _results.Values.ToList())
			{
				DrawResult(result);
				EditorGUILayout.Space();
			}
		}

		private void DrawResult(ModelResult result)
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.BeginHorizontal();

			var preview = PreviewFor(result);
			if (preview != null)
			{
				GUILayout.Label(preview, GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));
			}
			else
			{
				GUILayout.Box("no preview", GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));
			}

			EditorGUILayout.BeginVertical();
			EditorGUILayout.LabelField($"{result.AssetId} — {result.Status}", EditorStyles.boldLabel);

			if (result.Error.Length > 0)
			{
				EditorGUILayout.HelpBox(result.Error, MessageType.Error);
			}

			foreach (var issue in result.Report.Issues.Take(6))
			{
				EditorGUILayout.LabelField(issue.ToString(), EditorStyles.miniLabel);
			}

			if (result.Report.Issues.Count > 6)
			{
				EditorGUILayout.LabelField($"... and {result.Report.Issues.Count - 6} more", EditorStyles.miniLabel);
			}

			EditorGUILayout.BeginHorizontal();
			using (new EditorGUI.DisabledScope(_isRunning || result.Export == null))
			{
				if (GUILayout.Button("Accept", GUILayout.Width(90)))
				{
					Accept(result);
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
					RunSingleAssetAsync(result.AssetId, note);
				}
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndVertical();
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndVertical();
		}

		private void DrawUsage()
		{
			var snapshot = _usage.Snapshot();
			if (snapshot.Count == 0)
			{
				return;
			}

			EditorGUILayout.LabelField("Token usage", EditorStyles.boldLabel);
			foreach (var stage in snapshot)
			{
				EditorGUILayout.LabelField(
					$"{stage.Stage}: {stage.Requests} request(s), in {stage.Tokens.InputTokens:n0} " +
					$"(cache r {stage.Tokens.CacheReadInputTokens:n0} / w {stage.Tokens.CacheCreationInputTokens:n0}), " +
					$"out {stage.Tokens.OutputTokens:n0}",
					EditorStyles.miniLabel);
			}
		}

		private void DrawLog()
		{
			if (_log.Length == 0)
			{
				return;
			}

			EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
			_logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(80), GUILayout.MaxHeight(160));
			EditorGUILayout.SelectableLabel(_log.ToString(), EditorStyles.textArea, GUILayout.ExpandHeight(true));
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

		private void Accept(ModelResult result)
		{
			if (result.Export == null)
			{
				return;
			}

			var directory = Path.Combine(_outputFolder, result.AssetId);
			result.Export.WriteToDisk(directory);
			AssetDatabase.Refresh();
			Log($"{result.AssetId}: accepted -> {directory}");
		}

		private async void RunGenerateManifestAsync()
		{
			_isRunning = true;
			_cts = new CancellationTokenSource();
			try
			{
				using var gateway = new AnthropicGateway(_apiKey, _usage);
				var generator = new ManifestGenerator(gateway, VoxelizationConfig.Default);
				Log("Generating manifest...");
				var manifest = await generator.GenerateAsync(_gameBrief, _cts.Token);
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

			_isRunning = true;
			_cts = new CancellationTokenSource();
			_results.Clear();
			ClearPreviews();
			_usage = new TokenUsageTracker();

			try
			{
				using var gateway = new AnthropicGateway(_apiKey, _usage);
				var orchestrator = NewOrchestrator(gateway);
				foreach (var asset in manifest.Assets)
				{
					_cts.Token.ThrowIfCancellationRequested();
					var result = await orchestrator.RunAssetAsync(manifest, asset, string.Empty, _cts.Token, NewProgress());
					StoreResult(result);
				}

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

			_isRunning = true;
			_cts = new CancellationTokenSource();
			try
			{
				using var gateway = new AnthropicGateway(_apiKey, _usage);
				var orchestrator = NewOrchestrator(gateway);
				var result = await orchestrator.RunAssetAsync(manifest, asset, refinementNote, _cts.Token, NewProgress());
				StoreResult(result);
				Log($"{assetId}: done ({result.Status}).");
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

		private SetOrchestrator NewOrchestrator(AnthropicGateway gateway)
		{
			var config = VoxelizationConfig.Default;
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
			Repaint();
		});

		private void StoreResult(ModelResult result)
		{
			_results[result.AssetId] = result;
			if (_previews.TryGetValue(result.AssetId, out var old) && old != null)
			{
				DestroyImmediate(old);
			}

			_previews.Remove(result.AssetId);
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
			_cts?.Dispose();
			_cts = null;
			Repaint();
		}

		private void Log(string message)
		{
			_log.AppendLine(message);
			Repaint();
		}
	}
}
