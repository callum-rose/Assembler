using System;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Assembler.Generation.Verification.Editor
{
	/// <summary>
	/// Editor window that generates a game descriptor AND the assets it references in
	/// one pass. Separate from the descriptor-only window; shares the same API-key pref.
	/// </summary>
	public sealed class GameWithAssetsWindow : EditorWindow, IGeneratorLogger
	{
		// Shared with GameDescriptorGeneratorWindow so the key is entered once.
		private const string ApiKeyPref = "Assembler.Generation.ApiKey";
		private const string PromptPref = "Assembler.Generation.Assets.LastPrompt";
		private const string MaxAttemptsPref = "Assembler.Generation.Assets.MaxAttempts";
		private const string ConcurrencyPref = "Assembler.Generation.Assets.Concurrency";

		private string _prompt = string.Empty;
		private string _apiKey = string.Empty;
		private int _maxAttempts = 3;
		private int _concurrency = 4;

		private readonly StringBuilder _log = new();
		private Vector2 _logScroll;
		private Vector2 _promptScroll;
		private Vector2 _summaryScroll;

		private GameWithAssetsResult? _lastResult;
		private bool _isRunning;
		private CancellationTokenSource? _cts;

		[MenuItem("Assembler/Generate Game + Assets")]
		public static void Open()
		{
			var window = GetWindow<GameWithAssetsWindow>("Generate Game + Assets");
			window.minSize = new Vector2(520, 640);
			window.Show();
		}

		private void OnEnable()
		{
			_apiKey = EditorPrefs.GetString(ApiKeyPref, string.Empty);
			_prompt = EditorPrefs.GetString(PromptPref, string.Empty);
			_maxAttempts = Mathf.Max(1, EditorPrefs.GetInt(MaxAttemptsPref, 3));
			_concurrency = Mathf.Max(1, EditorPrefs.GetInt(ConcurrencyPref, 4));
		}

		private void OnDisable()
		{
			_cts?.Cancel();
		}

		private void OnGUI()
		{
			EditorGUILayout.LabelField("API key (shared, stored in EditorPrefs)", EditorStyles.boldLabel);
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_apiKey = EditorGUILayout.PasswordField(_apiKey);
				if (scope.changed) EditorPrefs.SetString(ApiKeyPref, _apiKey);
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Max attempts", EditorStyles.boldLabel);
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_maxAttempts = Mathf.Max(1, EditorGUILayout.IntField(_maxAttempts));
				if (scope.changed) EditorPrefs.SetInt(MaxAttemptsPref, _maxAttempts);
			}

			EditorGUILayout.LabelField("Asset generation concurrency", EditorStyles.boldLabel);
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_concurrency = Mathf.Clamp(EditorGUILayout.IntField(_concurrency), 1, 16);
				if (scope.changed) EditorPrefs.SetInt(ConcurrencyPref, _concurrency);
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Prompt", EditorStyles.boldLabel);
			_promptScroll = EditorGUILayout.BeginScrollView(_promptScroll, GUILayout.MinHeight(80), GUILayout.MaxHeight(160));
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_prompt = EditorGUILayout.TextArea(_prompt, GUILayout.ExpandHeight(true));
				if (scope.changed) EditorPrefs.SetString(PromptPref, _prompt);
			}
			EditorGUILayout.EndScrollView();

			EditorGUILayout.Space();
			using (new EditorGUI.DisabledScope(_isRunning))
			{
				if (GUILayout.Button(_isRunning ? "Generating..." : "Generate"))
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

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
			_logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(120), GUILayout.MaxHeight(220));
			EditorGUILayout.TextArea(_log.ToString(), GUILayout.ExpandHeight(true));
			EditorGUILayout.EndScrollView();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Result summary", EditorStyles.boldLabel);
			_summaryScroll = EditorGUILayout.BeginScrollView(_summaryScroll, GUILayout.MinHeight(120));
			EditorGUILayout.TextArea(BuildSummaryText(), GUILayout.ExpandHeight(true));
			EditorGUILayout.EndScrollView();
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
			_lastResult = null;
			_isRunning = true;
			_cts = new CancellationTokenSource();

			RunAsync(_cts.Token);
		}

		private async void RunAsync(CancellationToken ct)
		{
			try
			{
				var orchestrator = new GameWithAssetsOrchestrator(
					_apiKey, this, AssetGenerationOptions.Default, _concurrency);
				var result = await orchestrator.GenerateAsync(_prompt, _maxAttempts, ct);
				_lastResult = result;

				var assetSummary = SummariseAssets(result);
				switch (result.Generation)
				{
					case SuccessfulGeneration success:
						Log($"DONE — descriptor at {success.YamlPath}. {assetSummary}");
						break;
					case FailedGeneration failed:
						Log($"FAILED after {failed.Attempts.Count} attempt(s). " +
							$"YAML (last attempt): {failed.YamlPath ?? "<not written>"}. {assetSummary}");
						break;
				}
			}
			catch (OperationCanceledException)
			{
				Log("Cancelled.");
			}
			catch (Exception ex)
			{
				Log("Unexpected error: " + ex);
			}
			finally
			{
				_isRunning = false;
				_cts?.Dispose();
				_cts = null;
				Repaint();
			}
		}

		public void Log(string message)
		{
			_log.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ").AppendLine(message);
			Repaint();
		}

		private static string SummariseAssets(GameWithAssetsResult result)
		{
			var total = result.Assets.Count;
			if (total == 0) return "No generated assets.";
			var ok = 0;
			foreach (var a in result.Assets) if (a.Success) ok++;
			return $"Assets: {ok}/{total} generated.";
		}

		private string BuildSummaryText()
		{
			if (_lastResult == null)
			{
				return "(no result yet — run a generation)";
			}

			var sb = new StringBuilder();
			sb.AppendLine("=== Assets ===");
			if (_lastResult.Assets.Count == 0)
			{
				sb.AppendLine("(none generated)");
			}
			else
			{
				foreach (var a in _lastResult.Assets)
				{
					sb.Append(a.Success ? "[ok]  " : "[FAIL] ");
					sb.Append(a.Request.Type).Append(' ').Append(a.Request.Id);
					sb.Append("  -> Assets/Resources/").Append(a.Request.ResourcesPath).Append(".vox");
					if (!a.Success && !string.IsNullOrEmpty(a.Error))
					{
						sb.Append("  (").Append(a.Error).Append(')');
					}
					sb.AppendLine();
				}
			}

			sb.AppendLine();
			sb.AppendLine("=== Attempts ===");
			foreach (var attempt in _lastResult.Generation.Attempts)
			{
				var label = attempt.AttemptNumber == 1
					? "Attempt 1"
					: $"Attempt {attempt.AttemptNumber} (fix-up)";
				sb.AppendLine("--- " + label + " ---");
				switch (attempt)
				{
					case RequestFailedAttempt failed:
						sb.AppendLine("request error: " + failed.Error);
						break;
					case InvalidResponseAttempt invalid:
						sb.AppendLine(!string.IsNullOrWhiteSpace(invalid.Feedback) ? invalid.Feedback : "(no feedback)");
						sb.AppendLine("error: " + invalid.Error);
						break;
					case BuildAttempt build:
						sb.AppendLine(!string.IsNullOrWhiteSpace(build.Feedback) ? build.Feedback : "(no feedback)");
						if (!build.BuildResult.Success)
						{
							sb.AppendLine("build errors:");
							foreach (var e in build.BuildResult.Errors)
								sb.AppendLine("- " + e);
						}
						break;
				}
				sb.AppendLine();
			}

			return sb.ToString();
		}
	}
}
