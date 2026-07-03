using System;
using System.Collections.Generic;
using System.Threading;
using Assembler.Anthropic;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike.Generation
{
    /// <summary>
    /// Manual harness for the AI model-config layer: enter an asset description + the shared art
    /// blurb, hit Choose, and inspect the returned image prompt, applied rule ids, and resolved
    /// settings. It only displays the config — nothing is converted here.
    /// </summary>
    public sealed class ConfigGeneratorWindow : EditorWindow
    {
        // Shared with the descriptor generator window so the key is entered once.
        private const string ApiKeyPref = "Assembler.Generation.ApiKey";
        private const string ModelPref = "Assembler.AssetGeneration.MeshToVoxelSpike.Generation.Model";
        private const string ArtContextPref = "Assembler.AssetGeneration.MeshToVoxelSpike.Generation.ArtContext";
        private const string DescriptionPref = "Assembler.AssetGeneration.MeshToVoxelSpike.Generation.Description";

        // Config generation benefits from the strongest model; the picker can override it.
        private const string DefaultModel = "claude-opus-4-8";

        private string _apiKey = string.Empty;
        private string _model = DefaultModel;
        private string _artContext = string.Empty;
        private string _description = string.Empty;

        private readonly List<string> _models = new();

        private string _status = string.Empty;
        private ModelConfig? _result;
        private string _settingsDump = string.Empty;
        private string _meshyDump = string.Empty;
        private string _configJson = string.Empty;
        private Vector2 _scroll;
        private bool _isRunning;
        private CancellationTokenSource? _cts;

        [MenuItem("Window/Voxels/AI Model Config")]
        public static void Open()
        {
            var window = GetWindow<ConfigGeneratorWindow>("AI Model Config");
            window.minSize = new Vector2(520, 560);
            window.Show();
        }

        private void OnEnable()
        {
            _apiKey = EditorPrefs.GetString(ApiKeyPref, string.Empty);
            _model = EditorPrefs.GetString(ModelPref, DefaultModel);
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
            DrawModelSelector();

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
            EditorGUILayout.LabelField("Style rules", EditorStyles.boldLabel);
            var wrapMiniLabel = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            EditorGUILayout.LabelField(
                "The per-object-type rules that guide settings and prompt choices live in the " +
                "VoxelStyleRules JSON. Edit them there and re-run Choose to pick up changes.",
                wrapMiniLabel);
            if (GUILayout.Button("Edit style rules (JSON)"))
            {
                OpenStyleRulesJson();
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

                EditorGUILayout.LabelField("Base name", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    string.IsNullOrEmpty(result.BaseName) ? "(none)" : result.BaseName, wrapLabel);

                EditorGUILayout.LabelField("Applied rule ids", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    result.AppliedRuleIds.Count > 0 ? string.Join(", ", result.AppliedRuleIds) : "(none)",
                    wrapLabel);

                EditorGUILayout.LabelField("Resolved voxel settings", EditorStyles.boldLabel);
                DrawWrappedReadonly(_settingsDump, wrapArea);

                EditorGUILayout.LabelField("Resolved Meshy settings", EditorStyles.boldLabel);
                DrawWrappedReadonly(_meshyDump, wrapArea);

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

        private void DrawModelSelector()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("AI model", EditorStyles.boldLabel);
                if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                {
                    RefreshModels();
                }
            }

            // Popup over whatever models we know about; always include the current selection so a
            // stored/typed id stays selectable before any Refresh has run.
            var options = _models.Count > 0 ? new List<string>(_models) : new List<string>();
            if (!options.Contains(_model))
            {
                options.Insert(0, _model);
            }

            var index = options.IndexOf(_model);
            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                var picked = EditorGUILayout.Popup(index, options.ToArray());
                if (scope.changed && picked >= 0 && picked < options.Count)
                {
                    _model = options[picked];
                    EditorPrefs.SetString(ModelPref, _model);
                }
            }
        }

        // Opens the shared style-rules JSON in the project's configured script editor (Preferences >
        // External Tools) so the rules can be edited without leaving the flow.
        private void OpenStyleRulesJson()
        {
            var asset = Resources.Load<TextAsset>(StyleRules.ResourcePath);
            if (asset == null)
            {
                _status = $"ERROR: style-rules resource '{StyleRules.ResourcePath}' not found.";
                return;
            }

            if (!AssetDatabase.OpenAsset(asset))
            {
                _status = "Couldn't open the style-rules JSON in the external editor.";
                return;
            }

            _status = "Opened style rules in your editor.";
        }

        private async void RefreshModels()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _status = "ERROR: enter an API key before refreshing models.";
                Repaint();
                return;
            }

            try
            {
                _status = "Fetching available models...";
                Repaint();
                var ids = await AnthropicClient.ListModelsAsync(_apiKey);
                _models.Clear();
                _models.AddRange(ids);
                _status = _models.Count > 0 ? $"Loaded {_models.Count} models." : "No models returned.";
            }
            catch (Exception ex)
            {
                _status = "Error fetching models: " + ex.Message;
            }
            finally
            {
                Repaint();
            }
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
                using var client = new AnthropicClient(_apiKey, _model);
                var generator = new ModelConfigGenerator(client);
                var result = await generator.ChooseAsync(_description, _artContext, ct);
                _result = result;
                // Settings is an engine-free struct (no serializable fields), so dump the mutable DTO mirror.
                _settingsDump = JsonUtility.ToJson(SettingsConfig.From(result.Settings), prettyPrint: true);
                _meshyDump = JsonUtility.ToJson(result.Meshy, prettyPrint: true);
                _configJson = ConfigExtractor.Extract(result.RawText) ?? result.RawText;
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
