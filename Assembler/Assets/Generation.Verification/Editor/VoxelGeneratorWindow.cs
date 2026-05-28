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
		private const string NamePref = "Assembler.Voxels.LastName";
		private const string OutputFolderPref = "Assembler.Voxels.OutputFolder";
		private const string DefaultOutputFolder = "Assets/Resources/Voxels/";

		private string _apiKey = string.Empty;
		private string _prompt = string.Empty;
		private string _name = "voxel";
		private string _outputFolder = DefaultOutputFolder;
		private string _goxelText = string.Empty;

		private readonly StringBuilder _log = new();
		private Vector2 _logScroll;
		private Vector2 _promptScroll;
		private Vector2 _goxelScroll;
		private bool _isRunning;
		private CancellationTokenSource? _cts;

		[MenuItem("Assembler/Generate Voxel Mesh")]
		public static void Open()
		{
			var window = GetWindow<VoxelGeneratorWindow>("Generate Voxel");
			window.minSize = new Vector2(520, 700);
			window.Show();
		}

		private void OnEnable()
		{
			_apiKey = EditorPrefs.GetString(ApiKeyPref, string.Empty);
			_prompt = EditorPrefs.GetString(PromptPref, string.Empty);
			_name = EditorPrefs.GetString(NamePref, "voxel");
			_outputFolder = EditorPrefs.GetString(OutputFolderPref, DefaultOutputFolder);
		}

		private void OnDisable()
		{
			_cts?.Cancel();
		}

		private void OnGUI()
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
				if (GUILayout.Button(_isRunning ? "Generating..." : "1. Generate Goxel text"))
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
			EditorGUILayout.LabelField("Goxel text (editable — manual review gap)", EditorStyles.boldLabel);
			_goxelScroll = EditorGUILayout.BeginScrollView(_goxelScroll, GUILayout.MinHeight(160));
			_goxelText = EditorGUILayout.TextArea(_goxelText, GUILayout.ExpandHeight(true));
			EditorGUILayout.EndScrollView();

			using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_goxelText) || _isRunning))
			{
				if (GUILayout.Button("2. Convert to .vox"))
				{
					ConvertToVox();
				}
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
			_logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(120));
			EditorGUILayout.TextArea(_log.ToString(), GUILayout.ExpandHeight(true));
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
				var pipeline = new VoxelPipeline();
				var goxelText = await pipeline.GenerateGoxelTextAsync(_prompt, client, ct);
				_goxelText = goxelText;

				var txtPath = WriteText(goxelText, ".txt");
				Log($"Wrote {txtPath}");
				Log("Inspect/edit the Goxel text, then press '2. Convert to .vox'.");
				AssetDatabase.Refresh();
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

		private void ConvertToVox()
		{
			try
			{
				var pipeline = new VoxelPipeline();
				var bytes = pipeline.GoxelTextToVox(_goxelText);
				var voxPath = WriteBytes(bytes, ".vox");
				Log($"Wrote {voxPath}");
				AssetDatabase.Refresh();
			}
			catch (Exception ex)
			{
				Log("Convert failed: " + ex);
			}
		}

		private string WriteText(string text, string extension)
		{
			Directory.CreateDirectory(_outputFolder);
			var path = Path.Combine(_outputFolder, _name + extension);
			File.WriteAllText(path, text);
			return path;
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
	}
}
