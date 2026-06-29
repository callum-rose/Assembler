using System;
using System.Threading;
using Assembler.Anthropic;
using UnityEditor;
using UnityEngine;

namespace Assembler.VoxelPipeline.Generation
{
    /// <summary>
    /// Manual harness for the AI model-config layer: enter an asset description + the shared art
    /// blurb, hit Choose, and inspect the returned image prompt, applied rule ids, preset,
    /// resolution and resolved settings. It only displays the config — nothing is converted here.
    /// </summary>
    public sealed class VoxConfigGeneratorWindow : EditorWindow
    {
        // Shared with the descriptor generator window so the key is entered once.
        private const string ApiKeyPref = "Assembler.Generation.ApiKey";
        private const string ArtContextPref = "Assembler.VoxelPipeline.Generation.ArtContext";
        private const string DescriptionPref = "Assembler.VoxelPipeline.Generation.Description";

        private string _apiKey = string.Empty;
        private string _artContext = string.Empty;
        private string _description = string.Empty;

        private string _status = string.Empty;
        private VoxModelConfig? _result;
        private string _settingsDump = string.Empty;
        private Vector2 _scroll;
        private bool _isRunning;
        private CancellationTokenSource? _cts;

        [MenuItem("Window/Voxels/AI Model Config")]
        public static void Open()
        {
            var window = GetWindow<VoxConfigGeneratorWindow>("AI Model Config");
            window.minSize = new Vector2(520, 560);
            window.Show();
        }

        private void OnEnable()
        {
            _apiKey = EditorPrefs.GetString(ApiKeyPref, string.Empty);
            _artContext = EditorPrefs.GetString(ArtContextPref, string.Empty);
            _description = EditorPrefs.GetString(DescriptionPref, string.Empty);
        }

        private void OnDisable() => _cts?.Cancel();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("API key (stored in EditorPrefs)", EditorStyles.boldLabel);
            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                _apiKey = EditorGUILayout.PasswordField(_apiKey);
                if (scope.changed)
                {
                    EditorPrefs.SetString(ApiKeyPref, _apiKey);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shared art direction", EditorStyles.boldLabel);
            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                _artContext = EditorGUILayout.TextArea(_artContext, GUILayout.MinHeight(60));
                if (scope.changed)
                {
                    EditorPrefs.SetString(ArtContextPref, _artContext);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Model description", EditorStyles.boldLabel);
            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(60));
                if (scope.changed)
                {
                    EditorPrefs.SetString(DescriptionPref, _description);
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_isRunning))
            {
                if (GUILayout.Button(_isRunning ? "Choosing..." : "Choose"))
                {
                    StartChoose();
                }
            }
            using (new EditorGUI.DisabledScope(!_isRunning))
            {
                if (GUILayout.Button("Cancel"))
                {
                    _cts?.Cancel();
                }
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }

            if (_result is { } result)
            {
                EditorGUILayout.Space();
                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                EditorGUILayout.LabelField("Image prompt", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(result.ImagePrompt, EditorStyles.textArea, GUILayout.MinHeight(80));

                EditorGUILayout.LabelField("Applied rule ids", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(result.AppliedRuleIds.Count > 0
                    ? string.Join(", ", result.AppliedRuleIds)
                    : "(none)");

                EditorGUILayout.LabelField("Preset", result.Preset.ToString());
                EditorGUILayout.LabelField("Resolution", result.Resolution.ToString());

                EditorGUILayout.LabelField("Resolved settings", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(_settingsDump, EditorStyles.textArea, GUILayout.MinHeight(200));

                EditorGUILayout.EndScrollView();
            }
        }

        private void StartChoose()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _status = "ERROR: API key is required.";
                return;
            }
            if (string.IsNullOrWhiteSpace(_description))
            {
                _status = "ERROR: model description is required.";
                return;
            }

            _result = null;
            _settingsDump = string.Empty;
            _status = "Contacting Claude...";
            _isRunning = true;
            _cts = new CancellationTokenSource();
            ChooseAsync(_cts.Token);
        }

        private async void ChooseAsync(CancellationToken ct)
        {
            try
            {
                using var client = new AnthropicClient(_apiKey);
                var generator = new VoxModelConfigGenerator(client);
                var result = await generator.ChooseAsync(_description, _artContext, ct);
                _result = result;
                _settingsDump = JsonUtility.ToJson(result.Settings, prettyPrint: true);
                _status = "Done.";
            }
            catch (OperationCanceledException)
            {
                _status = "Cancelled.";
            }
            catch (Exception ex)
            {
                _status = "Error: " + ex.Message;
            }
            finally
            {
                _isRunning = false;
                _cts?.Dispose();
                _cts = null;
                Repaint();
            }
        }
    }
}
