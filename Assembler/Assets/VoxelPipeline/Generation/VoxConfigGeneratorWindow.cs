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
        private string _configJson = string.Empty;
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
            // Word-wrapping variants of the editable text area and the read-only labels, so long
            // prompts/settings wrap instead of running off the side. The whole window sits in one
            // scroll view so it scrolls vertically once the content outgrows the window.
            var wrapArea = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            var wrapLabel = new GUIStyle(EditorStyles.label) { wordWrap = true };

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

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
                _artContext = EditorGUILayout.TextArea(_artContext, wrapArea, GUILayout.MinHeight(60));
                if (scope.changed)
                {
                    EditorPrefs.SetString(ArtContextPref, _artContext);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Model description", EditorStyles.boldLabel);
            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                _description = EditorGUILayout.TextArea(_description, wrapArea, GUILayout.MinHeight(60));
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

                EditorGUILayout.LabelField("Image prompt", EditorStyles.boldLabel);
                DrawWrappedReadonly(result.ImagePrompt, wrapArea);

                EditorGUILayout.LabelField("Applied rule ids", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    result.AppliedRuleIds.Count > 0 ? string.Join(", ", result.AppliedRuleIds) : "(none)",
                    wrapLabel);

                EditorGUILayout.LabelField("Preset", result.Preset.ToString());
                EditorGUILayout.LabelField("Resolution", result.Resolution.ToString());

                EditorGUILayout.LabelField("Resolved settings", EditorStyles.boldLabel);
                DrawWrappedReadonly(_settingsDump, wrapArea);

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Config JSON", EditorStyles.boldLabel);
                    if (GUILayout.Button("Copy", GUILayout.Width(60)))
                    {
                        EditorGUIUtility.systemCopyBuffer = _configJson;
                        _status = "Config JSON copied to clipboard.";
                    }
                }
                EditorGUILayout.LabelField(
                    "Paste this into the \"Text to Voxels (pipeline)\" window's Import box.",
                    EditorStyles.miniLabel);
                DrawWrappedReadonly(_configJson, wrapArea);
            }

            EditorGUILayout.EndScrollView();
        }

        // Read-only, word-wrapped text sized to its content height so it shows in full and the
        // window's scroll view (not an inner one) handles any overflow.
        private static void DrawWrappedReadonly(string text, GUIStyle wrapStyle)
        {
            var width = EditorGUIUtility.currentViewWidth - 40f;
            var height = wrapStyle.CalcHeight(new GUIContent(text), width);
            EditorGUILayout.SelectableLabel(text, wrapStyle, GUILayout.Height(height));
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
                _configJson = VoxConfigExtractor.Extract(result.RawText) ?? result.RawText;
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
