using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Assembler.Generation.Verification.Editor
{
	/// <summary>
	/// Editor window that generates a game descriptor AND the assets it references in
	/// one pass. Separate from the descriptor-only window; shares the same API-key pref.
	/// Supports revising the last generated game with a follow-up instruction.
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

		private string _revisePrompt = string.Empty;

		private readonly StringBuilder _log = new();
		private Vector2 _logScroll;
		private Vector2 _promptScroll;
		private Vector2 _reviseScroll;
		private Vector2 _summaryScroll;
		private bool _summaryExpanded = true;

		private GameWithAssetsOrchestrator? _orchestrator;
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
				if (scope.changed)
				{
					EditorPrefs.SetString(ApiKeyPref, _apiKey);
				}
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Max attempts", EditorStyles.boldLabel);
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_maxAttempts = Mathf.Max(1, EditorGUILayout.IntField(_maxAttempts));
				if (scope.changed)
				{
					EditorPrefs.SetInt(MaxAttemptsPref, _maxAttempts);
				}
			}

			EditorGUILayout.LabelField("Asset generation concurrency", EditorStyles.boldLabel);
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_concurrency = Mathf.Clamp(EditorGUILayout.IntField(_concurrency), 1, 16);
				if (scope.changed)
				{
					EditorPrefs.SetInt(ConcurrencyPref, _concurrency);
				}
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Prompt", EditorStyles.boldLabel);
			_promptScroll = EditorGUILayout.BeginScrollView(_promptScroll, GUILayout.MinHeight(80), GUILayout.MaxHeight(160));
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

			DrawReviseSection();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
			_logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(120), GUILayout.MaxHeight(220));
			EditorGUILayout.TextArea(_log.ToString(), GUILayout.ExpandHeight(true));
			EditorGUILayout.EndScrollView();

			DrawSummarySection();
		}

		private void DrawReviseSection()
		{
			var canRevise = _orchestrator is { CanRevise: true };
			if (!canRevise)
			{
				return;
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Revise last game", EditorStyles.boldLabel);
			_reviseScroll = EditorGUILayout.BeginScrollView(_reviseScroll, GUILayout.MinHeight(50), GUILayout.MaxHeight(120));
			_revisePrompt = EditorGUILayout.TextArea(_revisePrompt, GUILayout.ExpandHeight(true));
			EditorGUILayout.EndScrollView();

			using (new EditorGUI.DisabledScope(_isRunning || string.IsNullOrWhiteSpace(_revisePrompt)))
			{
				if (GUILayout.Button(_isRunning ? "Revising..." : "Revise"))
				{
					StartRevise();
				}
			}
		}

		private void DrawSummarySection()
		{
			EditorGUILayout.Space();
			using (new EditorGUILayout.HorizontalScope())
			{
				_summaryExpanded = EditorGUILayout.Foldout(_summaryExpanded, "Result summary", true);
				GUILayout.FlexibleSpace();
				using (new EditorGUI.DisabledScope(_lastResult == null))
				{
					if (GUILayout.Button("Open summary in IDE", EditorStyles.miniButton, GUILayout.Width(150)))
					{
						OpenSummaryInIde();
					}

					var descriptorPath = _lastResult == null ? null : DescriptorPath(_lastResult);
					using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(descriptorPath)))
					{
						if (GUILayout.Button("Open descriptor in IDE", EditorStyles.miniButton, GUILayout.Width(160)))
						{
							OpenInIde(descriptorPath!);
						}
					}
				}
			}

			if (!_summaryExpanded)
			{
				return;
			}

			_summaryScroll = EditorGUILayout.BeginScrollView(_summaryScroll, GUILayout.MinHeight(120));
			EditorGUILayout.TextArea(BuildSummaryText(), WrapTextAreaStyle, GUILayout.ExpandHeight(true));
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
			_orchestrator = new GameWithAssetsOrchestrator(
				_apiKey, this, AssetGenerationOptions.Default, _concurrency);

			RunOperation(ct => _orchestrator.GenerateAsync(_prompt, _maxAttempts, ct));
		}

		private void StartRevise()
		{
			if (_orchestrator is not { CanRevise: true })
			{
				Log("ERROR: nothing to revise yet — generate a game first.");
				return;
			}
			if (string.IsNullOrWhiteSpace(_revisePrompt))
			{
				Log("ERROR: revision instruction is required.");
				return;
			}

			var instruction = _revisePrompt;
			RunOperation(ct => _orchestrator.ReviseAsync(instruction, _maxAttempts, ct));
		}

		private async void RunOperation(Func<CancellationToken, Task<GameWithAssetsResult>> operation)
		{
			_isRunning = true;
			_cts = new CancellationTokenSource();

			try
			{
				var result = await operation(_cts.Token);
				_lastResult = result;

				var assetSummary = SummariseAssets(result);
				switch (result.Generation)
				{
					case SuccessfulGeneration success:
						Log($"DONE — descriptor at {success.YamlPath}. {assetSummary}");
						_revisePrompt = string.Empty;
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

		private void OpenSummaryInIde()
		{
			try
			{
				var path = Path.Combine(Application.temporaryCachePath, "GameWithAssets-summary.txt");
				File.WriteAllText(path, BuildSummaryText());
				OpenInIde(path);
			}
			catch (Exception ex)
			{
				Log("Could not open summary: " + ex.Message);
			}
		}

		private void OpenInIde(string path)
		{
			if (!InternalEditorUtility.OpenFileAtLineExternal(path, 0))
			{
				Log($"Could not open '{path}' in the external editor.");
			}
		}

		private static string? DescriptorPath(GameWithAssetsResult result) =>
			result.Generation switch
			{
				SuccessfulGeneration s => s.YamlPath,
				FailedGeneration f => f.YamlPath,
				_ => null,
			};

		private static string SummariseAssets(GameWithAssetsResult result)
		{
			var total = result.Assets.Count;
			if (total == 0)
			{
				return "No generated assets.";
			}

			var ok = 0;
			foreach (var a in result.Assets)
			{
				if (a.Success)
				{
					ok++;
				}
			}

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
							{
								sb.AppendLine("- " + e);
							}
						}
						break;
				}
				sb.AppendLine();
			}

			return sb.ToString();
		}

		private static GUIStyle? _wrapTextAreaStyle;
		private static GUIStyle WrapTextAreaStyle => _wrapTextAreaStyle ??= new GUIStyle(EditorStyles.textArea)
		{
			wordWrap = true,
		};
	}
}
