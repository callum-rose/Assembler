using System;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Assembler.Generation.Verification.Editor
{
	public sealed class GameDescriptorGeneratorWindow : EditorWindow, IGeneratorLogger
	{
		private const string ApiKeyPref = "Assembler.Generation.ApiKey";
		private const string PromptPref = "Assembler.Generation.LastPrompt";
		private const string MaxAttemptsPref = "Assembler.Generation.MaxAttempts";

		private string _prompt = string.Empty;
		private string _apiKey = string.Empty;
		private int _maxAttempts = 3;

		private readonly StringBuilder _log = new();
		private Vector2 _logScroll;
		private Vector2 _promptScroll;
		private Vector2 _feedbackScroll;

		private GenerationResult? _lastResult;
		private bool _isRunning;
		private CancellationTokenSource? _cts;

		[MenuItem("Assembler/Generate Game Descriptor")]
		public static void Open()
		{
			var window = GetWindow<GameDescriptorGeneratorWindow>("Generate Game");
			window.minSize = new Vector2(520, 600);
			window.Show();
		}

		private void OnEnable()
		{
			_apiKey = EditorPrefs.GetString(ApiKeyPref, string.Empty);
			_prompt = EditorPrefs.GetString(PromptPref, string.Empty);
			_maxAttempts = Mathf.Max(1, EditorPrefs.GetInt(MaxAttemptsPref, 3));
		}

		private void OnDisable()
		{
			_cts?.Cancel();
		}

		private void OnGUI()
		{
			EditorGUILayout.LabelField("API key (stored in EditorPrefs)", EditorStyles.boldLabel);
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
			EditorGUILayout.LabelField("Claude's feedback", EditorStyles.boldLabel);
			_feedbackScroll = EditorGUILayout.BeginScrollView(_feedbackScroll, GUILayout.MinHeight(120));
			EditorGUILayout.TextArea(BuildFeedbackText(), GUILayout.ExpandHeight(true));
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
				var orchestrator = GenerationOrchestrator.CreateDefault(_apiKey, this);
				var result = await orchestrator.GenerateAsync(_prompt, _maxAttempts, ct);
				_lastResult = result;
				switch (result)
				{
					case SuccessfulGeneration success:
						Log($"DONE — descriptor at {success.YamlPath}");
						break;
					case FailedGeneration failed:
						Log($"FAILED after {failed.Attempts.Count} attempt(s). YAML (last attempt): {failed.YamlPath ?? "<not written>"}");
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

		private string BuildFeedbackText()
		{
			if (_lastResult == null || _lastResult.Attempts.Count == 0)
			{
				return "(no feedback yet — run a generation)";
			}

			var sb = new StringBuilder();
			foreach (var attempt in _lastResult.Attempts)
			{
				var label = attempt.AttemptNumber == 1 ? "Attempt 1 feedback" : $"Attempt {attempt.AttemptNumber} feedback (fix-up)";
				sb.AppendLine("=== " + label + " ===");
				switch (attempt)
				{
					case RequestFailedAttempt failed:
						sb.AppendLine("(empty)");
						sb.AppendLine("--- request error ---");
						sb.AppendLine(failed.Error);
						break;
					case InvalidResponseAttempt invalid:
						sb.AppendLine(!string.IsNullOrWhiteSpace(invalid.Feedback) ? invalid.Feedback : "(empty)");
						sb.AppendLine("--- request error ---");
						sb.AppendLine(invalid.Error);
						break;
					case BuildAttempt build:
						sb.AppendLine(!string.IsNullOrWhiteSpace(build.Feedback) ? build.Feedback : "(empty)");
						if (!build.BuildResult.Success)
						{
							sb.AppendLine("--- build errors ---");
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
