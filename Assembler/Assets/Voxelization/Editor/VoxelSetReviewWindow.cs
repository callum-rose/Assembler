using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;
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
		private const string UsePlanPref = "Assembler.Voxelization.UseClaudeCodePlan";
		private const string ManifestPref = "Assembler.Voxelization.Manifest";
		private const string BriefPref = "Assembler.Voxelization.GameBrief";
		private const string OutputFolderPref = "Assembler.Voxelization.OutputFolder";
		private const string ImageFolderPref = "Assembler.Voxelization.ImageFolder";
		private const string AdvancedFoldoutPref = "Assembler.Voxelization.ShowAdvanced";
		private const string VerboseLogPref = "Assembler.Voxelization.VerboseLog";
		private const string SidebarWidthPref = "Assembler.Voxelization.SidebarWidth";
		private const float PreviewSize = 200f;
		private const float DefaultSidebarWidth = 360f;
		private const float MinSidebarWidth = 240f;
		private const float SplitterWidth = 6f;

		// The log/sidebar pane width is operator-draggable (the silhouette ASCII
		// grids wrap when it is too narrow), persisted so it survives restarts.
		private float _sidebarWidth = DefaultSidebarWidth;
		private bool _draggingSidebar;

		// Fetched from the Anthropic models API on startup (newest first) rather
		// than hardcoded, so the picker stays current as new models ship. Models
		// released before the previous calendar year are dropped, keeping the
		// list to recent generations rather than every legacy id.
		private static DateTimeOffset ModelRecencyCutoff =>
			new(DateTime.UtcNow.Year - 1, 1, 1, 0, 0, 0, TimeSpan.Zero);

		private string[] _modelOptions = Array.Empty<string>();
		private bool _modelsLoading;
		// Derived UI state must not survive a domain reload: Unity's assembly-reload
		// serialisation backs up private fields too, so a null error would come back as
		// "" (a non-null empty box) unless it's explicitly excluded.
		[NonSerialized] private string? _modelsError;
		private CancellationTokenSource? _modelsCts;

		private string _apiKey = string.Empty;

		// When on, every LLM call routes through the `claude -p` CLI (the operator's
		// Claude subscription / plan) instead of the API, so a run bills the plan and
		// needs no API key. Backed by EditorPrefs so it survives restarts.
		private bool _usePlan;

		// The fixed CLI model aliases shown in place of the API-backed model list when
		// running on the plan — the CLI maps these (or a full model id) onto a model.
		private static readonly string[] PlanModelAliases = { "opus", "sonnet", "haiku" };

		private string _gameBrief = string.Empty;
		private string _manifestYaml = string.Empty;
		private string _outputFolder = "Assets/Resources/Voxels/Sets/";
		private string _imageFolder = string.Empty;

		// Stage models, style guidance and the granular retry/budget knobs are
		// persisted in this shared asset rather than per-machine EditorPrefs.
		private VoxelizationSettings _settings = null!;
		private bool _showAdvanced;

		private readonly Dictionary<string, ModelResult> _results = new();
		private readonly Dictionary<string, Texture2D> _previews = new();

		// Reference-image assignment panel. The dropdown selections per file
		// ((assetIndex, faceIndex), index 0 = none/unset) are the live editing
		// state; the manifest yaml above is the source of truth they round-trip
		// through. _referenceRowsSource is the manifest text the rows were last
		// derived from, so an external manifest edit re-syncs them but our own
		// re-serialisation does not clobber an in-progress (invalid) edit.
		private readonly Dictionary<string, (int AssetIndex, int FaceIndex)> _referenceRows = new();
		private readonly Dictionary<string, Texture2D?> _referenceThumbnails = new();
		private string _referenceRowsSource = string.Empty;
		private string _thumbnailFolder = string.Empty;
		// Transient validation error — kept out of reload serialisation so a cleared
		// (null) error doesn't return as "" and render a blank error box after a recompile.
		[NonSerialized] private string? _referenceAssignError;
		private Vector2 _referenceScroll;

		// DrawReferenceImages runs every OnGUI event, so the manifest parse and the
		// folder scan are cached: the manifest is re-parsed only when its text
		// changes, the file list re-scanned only when the folder changes (or on
		// OnFocus, so images added on disk still appear). The "\0" sentinels force
		// the first draw to populate both.
		//
		// These are [NonSerialized] so a domain reload (script recompile) resets them
		// to their initialisers. Unity's assembly-reload serialisation otherwise backs
		// up private fields, which restores _cachedReferenceManifestSource to the live
		// manifest text while the parsed _cachedReferenceManifest (a plain record, not
		// Unity-serialisable) returns null — leaving the cache "valid but empty" and
		// wrongly showing the "fix the manifest yaml" warning until the text is edited.
		[NonSerialized] private SetManifest? _cachedReferenceManifest;
		[NonSerialized] private string _cachedReferenceManifestSource = "\0";
		[NonSerialized] private string? _cachedReferenceManifestError;
		[NonSerialized] private IReadOnlyList<string> _cachedImageFiles = Array.Empty<string>();
		[NonSerialized] private string _cachedImageFilesFolder = "\0";
		private readonly Dictionary<string, string> _refineNotes = new();
		private readonly Dictionary<string, Vector2> _infoScrolls = new();
		private readonly Dictionary<string, string> _inFlight = new();

		// A refined model becomes a new gallery slot "{baseId}-v{n}" sitting next to
		// the original rather than overwriting it. This maps each revision slot back
		// to its base manifest asset so a revision can itself be re-refined; original
		// slots aren't listed here and resolve to themselves.
		private readonly Dictionary<string, string> _baseAssetId = new();
		// Two parallel streams captured every run: _log is the headline narrative;
		// _verboseLog is that same narrative with every LLM call's full
		// prompt/response/tool-use/usage interleaved in. Both are always recorded, so
		// the sidebar's Verbose toggle switches which one is shown after the fact
		// rather than having to be armed before the run.
		private readonly StringBuilder _log = new();
		private readonly StringBuilder _verboseLog = new();

		// Rebuilding the log string and recomputing its height every OnGUI is O(log
		// size) per frame — fine when the log was headlines, costly now that verbose
		// mode pours full prompts/responses into it. Cache both, invalidated on append
		// or when the toggle flips the active stream.
		private string _logText = string.Empty;
		private float _logHeight;
		private float _logMeasuredWidth = -1f;
		private bool _logDirty = true;
		private bool _showVerbose;
		private TokenUsageTracker _usage = new();

		private readonly System.Diagnostics.Stopwatch _runTimer = new();
		private string _statusLine = string.Empty;
		private string _runFolder = string.Empty;
		private string _runTimestamp = string.Empty;
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
			_usePlan = EditorPrefs.GetBool(UsePlanPref, false);
			_gameBrief = EditorPrefs.GetString(BriefPref, string.Empty);
			_manifestYaml = EditorPrefs.GetString(ManifestPref, string.Empty);
			_outputFolder = EditorPrefs.GetString(OutputFolderPref, _outputFolder);
			_imageFolder = EditorPrefs.GetString(ImageFolderPref, string.Empty);
			_showAdvanced = EditorPrefs.GetBool(AdvancedFoldoutPref, false);
			_showVerbose = EditorPrefs.GetBool(VerboseLogPref, true);
			_sidebarWidth = EditorPrefs.GetFloat(SidebarWidthPref, DefaultSidebarWidth);
			_settings = VoxelizationSettings.LoadOrCreate();
			EditorApplication.update += OnEditorUpdate;
			RefreshModels();
		}

		private void OnFocus()
		{
			// Re-scan the reference folder when the window regains focus, so images
			// added on disk appear without paying a per-frame directory scan.
			_cachedImageFilesFolder = "\0";
			_thumbnailFolder = "\0";
		}

		private void OnDisable()
		{
			EditorApplication.update -= OnEditorUpdate;
			_cts?.Cancel();
			_modelsCts?.Cancel();
			DisposeThumbnails();
			if (_settings != null)
			{
				AssetDatabase.SaveAssetIfDirty(_settings);
			}
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
			DrawReferenceImages();
			EditorGUILayout.Space();
			DrawRunControls();
			EditorGUILayout.Space();
			DrawGallery();
			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();

			DrawSidebarSplitter();

			EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(_sidebarWidth));
			DrawSidebar();
			EditorGUILayout.EndVertical();

			EditorGUILayout.EndHorizontal();
		}

		/// <summary>
		/// A draggable handle between the main content and the log sidebar. Dragging
		/// it left widens the sidebar so a silhouette ASCII grid that would otherwise
		/// wrap fits on one line; the chosen width is clamped to the window and
		/// persisted. Double-click resets it to the default.
		/// </summary>
		private void DrawSidebarSplitter()
		{
			var handle = GUILayoutUtility.GetRect(SplitterWidth, SplitterWidth, GUILayout.ExpandHeight(true), GUILayout.Width(SplitterWidth));
			EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeHorizontal);

			// A thin grab line so the handle is discoverable rather than invisible.
			var line = new Rect(handle.x + handle.width / 2f - 0.5f, handle.y, 1f, handle.height);
			EditorGUI.DrawRect(line, new Color(0f, 0f, 0f, 0.25f));

			var e = Event.current;
			if (e.type == EventType.MouseDown && e.button == 0 && handle.Contains(e.mousePosition))
			{
				if (e.clickCount == 2)
				{
					SetSidebarWidth(DefaultSidebarWidth);
					EditorPrefs.SetFloat(SidebarWidthPref, _sidebarWidth);
				}
				else
				{
					_draggingSidebar = true;
				}

				e.Use();
			}
			else if (_draggingSidebar && e.type == EventType.MouseDrag)
			{
				// The sidebar hugs the right edge, so its width grows as the pointer moves left.
				SetSidebarWidth(position.width - e.mousePosition.x - SplitterWidth / 2f);
				Repaint();
				e.Use();
			}
			else if (e.type == EventType.MouseUp && _draggingSidebar)
			{
				_draggingSidebar = false;
				EditorPrefs.SetFloat(SidebarWidthPref, _sidebarWidth);
				e.Use();
			}
		}

		private void SetSidebarWidth(float width) =>
			_sidebarWidth = Mathf.Clamp(width, MinSidebarWidth, Mathf.Max(MinSidebarWidth, position.width - 200f));

		private void OnEditorUpdate()
		{
			// Keep the timer and streaming status line moving while a run is live.
			if (_isRunning)
			{
				Repaint();
			}
		}

		private VoxelizationConfig BuildConfig() => _settings.ToConfig();

		private void DrawSettings()
		{
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_usePlan = EditorGUILayout.ToggleLeft(
					new GUIContent("Use Claude Code plan (no API key)",
						"Route every pipeline call through the `claude -p` CLI so the run bills your Claude " +
						"subscription instead of API credits. Requires the claude CLI on PATH (or CLAUDE_CLI_PATH set)."),
					_usePlan);
				if (scope.changed)
				{
					EditorPrefs.SetBool(UsePlanPref, _usePlan);
				}
			}

			if (_usePlan)
			{
				EditorGUILayout.HelpBox(
					"Running on the Claude plan via the claude CLI — no API key needed. Stage models use the fixed " +
					"opus/sonnet/haiku aliases below, and the cost panel shows the API-equivalent spend you saved.",
					MessageType.Info);
			}

			using (new EditorGUI.DisabledScope(_usePlan))
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

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Stage models", EditorStyles.boldLabel);
			GUILayout.FlexibleSpace();
			// The API models list is irrelevant on the plan — the CLI takes fixed aliases.
			if (!_usePlan)
			{
				using (new EditorGUI.DisabledScope(_modelsLoading || string.IsNullOrWhiteSpace(_apiKey)))
				{
					if (GUILayout.Button(_modelsLoading ? "Loading..." : "Refresh", GUILayout.Width(80)))
					{
						RefreshModels();
					}
				}
			}

			EditorGUILayout.EndHorizontal();

			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_settings.ManifestModel = DrawModelPopup("Manifest model", _settings.ManifestModel);
				_settings.PlanningModel = DrawModelPopup("Planning model", _settings.PlanningModel);
				_settings.AuthoringModel = DrawModelPopup("Authoring model", _settings.AuthoringModel);
				if (scope.changed)
				{
					EditorUtility.SetDirty(_settings);
				}
			}

			if (!_usePlan && _modelsError != null)
			{
				EditorGUILayout.HelpBox($"Couldn't load model list: {_modelsError}", MessageType.Warning);
			}

			DrawAdvancedSettings();
		}

		/// <summary>
		/// Collapsible section for the granular retry/budget knobs. Hidden by
		/// default — these are rarely touched — and persisted with the rest of the
		/// settings asset. Drawn through the asset's <see cref="SerializedObject"/>
		/// so each field honours its [Min]/[Range]/[Tooltip] attributes for free.
		/// </summary>
		private void DrawAdvancedSettings()
		{
			using var scope = new EditorGUI.ChangeCheckScope();
			_showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced settings", toggleOnLabelClick: true);
			if (scope.changed)
			{
				EditorPrefs.SetBool(AdvancedFoldoutPref, _showAdvanced);
			}

			if (!_showAdvanced)
			{
				return;
			}

			var serialized = new SerializedObject(_settings);
			using (new EditorGUI.IndentLevelScope())
			{
				DrawSettingsProperty(serialized, nameof(VoxelizationSettings.DeterministicBrief), "Deterministic brief (pixel silhouette + palette)");
				DrawSettingsProperty(serialized, nameof(VoxelizationSettings.ExtractSemanticBriefFields), "Extract semantic brief fields (vision call)");
				DrawSettingsProperty(serialized, nameof(VoxelizationSettings.MaxPartAttempts), "Max part attempts");
				DrawSettingsProperty(serialized, nameof(VoxelizationSettings.MaxValidationRounds), "Max validation rounds");
				DrawSettingsProperty(serialized, nameof(VoxelizationSettings.MaxReviewRounds), "Max review rounds");
				DrawSettingsProperty(serialized, nameof(VoxelizationSettings.PartVoxelBudget), "Part voxel budget");
				DrawSettingsProperty(serialized, nameof(VoxelizationSettings.SilhouetteIouThreshold), "Silhouette IoU threshold");
				DrawSettingsProperty(serialized, nameof(VoxelizationSettings.SilhouetteCoverageThreshold), "Silhouette coverage threshold");

				if (GUILayout.Button("Reset to defaults", GUILayout.Width(160)))
				{
					ResetAdvancedToDefaults(serialized);
				}
			}

			serialized.ApplyModifiedProperties();
		}

		private static void DrawSettingsProperty(SerializedObject serialized, string propertyName, string label) =>
			EditorGUILayout.PropertyField(serialized.FindProperty(propertyName), new GUIContent(label));

		private static void ResetAdvancedToDefaults(SerializedObject serialized)
		{
			var defaults = VoxelizationConfig.Default;
			serialized.FindProperty(nameof(VoxelizationSettings.DeterministicBrief)).boolValue = defaults.DeterministicBrief;
			serialized.FindProperty(nameof(VoxelizationSettings.ExtractSemanticBriefFields)).boolValue = defaults.ExtractSemanticBriefFields;
			serialized.FindProperty(nameof(VoxelizationSettings.MaxPartAttempts)).intValue = defaults.MaxPartAttempts;
			serialized.FindProperty(nameof(VoxelizationSettings.MaxValidationRounds)).intValue = defaults.MaxValidationRounds;
			serialized.FindProperty(nameof(VoxelizationSettings.MaxReviewRounds)).intValue = defaults.MaxReviewRounds;
			serialized.FindProperty(nameof(VoxelizationSettings.PartVoxelBudget)).intValue = defaults.PartVoxelBudget;
			serialized.FindProperty(nameof(VoxelizationSettings.SilhouetteIouThreshold)).floatValue = defaults.SilhouetteIouThreshold;
			serialized.FindProperty(nameof(VoxelizationSettings.SilhouetteCoverageThreshold)).floatValue = defaults.SilhouetteCoverageThreshold;
		}

		private string DrawModelPopup(string label, string current)
		{
			// On the plan the CLI takes a fixed alias list; off it, the API-fetched
			// model ids. Either way keep the saved selection in the list so the popup
			// can show it before the models load (or if it names one not in the list).
			var source = _usePlan ? PlanModelAliases : _modelOptions;
			var options = string.IsNullOrEmpty(current) || source.Contains(current)
				? source
				: source.Append(current).ToArray();

			if (options.Length == 0)
			{
				EditorGUILayout.LabelField(label, _modelsLoading ? "Loading models..." : "Enter an API key to load models");
				return current;
			}

			var index = Mathf.Max(0, Array.IndexOf(options, current));
			return options[EditorGUILayout.Popup(label, index, options)];
		}

		/// <summary>
		/// Pulls the current model list from the Anthropic API for the model
		/// pickers. No-op without an API key; failures surface as a warning rather
		/// than throwing, since the saved selections still drive a run.
		/// </summary>
		private async void RefreshModels()
		{
			if (_modelsLoading || string.IsNullOrWhiteSpace(_apiKey))
			{
				return;
			}

			_modelsCts?.Cancel();
			_modelsCts = new CancellationTokenSource();
			var token = _modelsCts.Token;

			_modelsLoading = true;
			_modelsError = null;
			Repaint();

			try
			{
				var models = await AnthropicClient.ListModelsAsync(_apiKey, ModelRecencyCutoff, token);
				if (token.IsCancellationRequested)
				{
					return;
				}

				_modelOptions = models.ToArray();
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				_modelsError = ex.Message;
			}
			finally
			{
				if (!token.IsCancellationRequested)
				{
					_modelsLoading = false;
					Repaint();
				}
			}
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

			using (new EditorGUI.DisabledScope(_isRunning || (!_usePlan && string.IsNullOrWhiteSpace(_apiKey)) || string.IsNullOrWhiteSpace(_gameBrief)))
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
				_settings.StyleGuidance = EditorGUILayout.TextArea(_settings.StyleGuidance, _wrappedTextArea, GUILayout.ExpandHeight(true));
				if (scope.changed)
				{
					EditorUtility.SetDirty(_settings);
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

		/// <summary>
		/// Assigns the reference-image folder's files to manifest assets, one
		/// (asset, face) per image. The manifest yaml is the source of truth
		/// (decision #1): rows initialise from its <c>references:</c> and any edit
		/// re-serialises the whole manifest back into the text buffer. Filenames are
		/// usually opaque hashes, so each row shows a thumbnail to make ruling out
		/// bad images usable; the perspective/asset are pre-filled from the filename
		/// where unambiguous (suggest-only, never auto-committed).
		/// </summary>
		private void DrawReferenceImages()
		{
			EditorGUILayout.LabelField("Reference images (assign to assets)", EditorStyles.boldLabel);

			if (string.IsNullOrWhiteSpace(_imageFolder) || !Directory.Exists(_imageFolder))
			{
				EditorGUILayout.HelpBox("Set a reference image folder above to assign images to assets.", MessageType.Info);
				return;
			}

			if (!TryGetCachedReferenceManifest(out var manifest))
			{
				EditorGUILayout.HelpBox(
					$"Assigning references needs the manifest yaml to parse — fix it first: {_cachedReferenceManifestError}",
					MessageType.Warning);
				return;
			}

			var files = CachedImageFiles();
			if (files.Count == 0)
			{
				EditorGUILayout.HelpBox("No images (.png/.jpg/.jpeg/.gif/.webp) in the folder.", MessageType.Info);
				return;
			}

			var assetIds = manifest.Assets.Select(a => a.Id).ToArray();
			var assetOptions = new[] { "(none — exclude)" }.Concat(assetIds).ToArray();
			var faceOptions = new[] { "(face?)" }.Concat(ReferenceImage.Faces).ToArray();
			SyncReferenceRows(manifest, files, assetIds);

			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_referenceScroll = EditorGUILayout.BeginScrollView(_referenceScroll, GUILayout.MinHeight(80), GUILayout.MaxHeight(320));
				foreach (var file in files)
				{
					var name = Path.GetFileName(file);
					var (assetIndex, faceIndex) = _referenceRows[name];

					EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
					var thumb = ThumbnailFor(file);
					GUILayout.Label(
						thumb != null ? new GUIContent(thumb) : new GUIContent(name),
						GUILayout.Width(64), GUILayout.Height(64));

					EditorGUILayout.BeginVertical();
					EditorGUILayout.LabelField(name, EditorStyles.miniLabel);
					assetIndex = EditorGUILayout.Popup("Asset", Mathf.Clamp(assetIndex, 0, assetOptions.Length - 1), assetOptions);
					faceIndex = EditorGUILayout.Popup("Perspective", Mathf.Clamp(faceIndex, 0, faceOptions.Length - 1), faceOptions);
					EditorGUILayout.EndVertical();
					EditorGUILayout.EndHorizontal();

					_referenceRows[name] = (assetIndex, faceIndex);
				}

				EditorGUILayout.EndScrollView();

				if (scope.changed)
				{
					CommitReferenceRows(manifest, assetIds);
				}
			}

			if (_referenceAssignError != null)
			{
				EditorGUILayout.HelpBox(_referenceAssignError, MessageType.Error);
			}
		}

		/// <summary>
		/// Re-derives the per-file dropdown state from the manifest whenever the
		/// manifest text changed externally (or a new file appeared). Existing
		/// <c>references:</c> entries win; unassigned files fall back to a
		/// suggest-only inference from the filename.
		/// </summary>
		private void SyncReferenceRows(SetManifest manifest, IReadOnlyList<string> files, string[] assetIds)
		{
			if (_referenceRowsSource == _manifestYaml &&
				files.All(f => _referenceRows.ContainsKey(Path.GetFileName(f))))
			{
				return;
			}

			var assigned = new Dictionary<string, (string Asset, string Face)>(StringComparer.OrdinalIgnoreCase);
			foreach (var asset in manifest.Assets)
			{
				foreach (var reference in asset.References)
				{
					assigned[reference.File] = (asset.Id, reference.Face);
				}
			}

			_referenceRows.Clear();
			foreach (var file in files)
			{
				var name = Path.GetFileName(file);
				if (assigned.TryGetValue(name, out var current))
				{
					var assetIndex = Array.IndexOf(assetIds, current.Asset);
					var faceIndex = ReferenceImage.FaceIndex(current.Face);
					_referenceRows[name] = (assetIndex >= 0 ? assetIndex + 1 : 0, faceIndex >= 0 ? faceIndex + 1 : 0);
				}
				else
				{
					_referenceRows[name] = InferReferenceRow(name, assetIds);
				}
			}

			_referenceRowsSource = _manifestYaml;
		}

		/// <summary>
		/// Suggest-only pre-fill from a filename: a face token (front/back/left/
		/// right/top/bottom, plus rear→back and underside→bottom) and a unique
		/// asset-id token match. Ambiguous/hash-named files stay unset (index 0).
		/// </summary>
		private static (int AssetIndex, int FaceIndex) InferReferenceRow(string fileName, string[] assetIds)
		{
			var separators = new[] { '_', '-', '.', ' ', '/' };
			var tokens = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant()
				.Split(separators, StringSplitOptions.RemoveEmptyEntries);

			var faceIndex = 0;
			foreach (var (token, face) in FaceTokens)
			{
				if (tokens.Contains(token))
				{
					faceIndex = ReferenceImage.FaceIndex(face) + 1;
					break;
				}
			}

			var matches = assetIds.Where(id => MatchesAsset(tokens, id)).ToList();
			var assetIndex = matches.Count == 1 ? Array.IndexOf(assetIds, matches[0]) + 1 : 0;
			return (assetIndex, faceIndex);
		}

		private static readonly (string Token, string Face)[] FaceTokens =
		{
			("front", "front"), ("back", "back"), ("rear", "back"),
			("left", "left"), ("right", "right"),
			("top", "top"), ("bottom", "bottom"), ("underside", "bottom"),
		};

		private static bool MatchesAsset(string[] fileTokens, string assetId)
		{
			var idTokens = assetId.ToLowerInvariant().Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			return idTokens.Length > 0 && idTokens.All(fileTokens.Contains);
		}

		/// <summary>
		/// Writes the current row selections back into the manifest yaml, one
		/// reference per (asset, face). A duplicate (asset, face) is an invalid
		/// state: it is flagged and the manifest is left untouched rather than
		/// silently replacing one image with another.
		/// </summary>
		private void CommitReferenceRows(SetManifest manifest, string[] assetIds)
		{
			var byAsset = new Dictionary<string, List<ReferenceImage>>();
			var seen = new HashSet<(string Asset, string Face)>();
			foreach (var entry in _referenceRows)
			{
				var (assetIndex, faceIndex) = entry.Value;
				if (assetIndex <= 0 || faceIndex <= 0)
				{
					continue;
				}

				var assetId = assetIds[assetIndex - 1];
				var face = ReferenceImage.Faces[faceIndex - 1];
				if (!seen.Add((assetId, face)))
				{
					_referenceAssignError =
						$"Two images both claim {assetId}'s {face} view. One image per (asset, face) — change one before it can be saved.";
					return;
				}

				if (!byAsset.TryGetValue(assetId, out var list))
				{
					byAsset[assetId] = list = new List<ReferenceImage>();
				}

				list.Add(new ReferenceImage(entry.Key, face));
			}

			_referenceAssignError = null;
			var updated = manifest with
			{
				Assets = manifest.Assets
					.Select(a => a with
					{
						References = byAsset.TryGetValue(a.Id, out var refs)
							? refs
							: (IReadOnlyList<ReferenceImage>)Array.Empty<ReferenceImage>(),
					})
					.ToArray(),
			};

			_manifestYaml = ManifestYaml.Write(updated);
			_referenceRowsSource = _manifestYaml;
			EditorPrefs.SetString(ManifestPref, _manifestYaml);
		}

		/// <summary>
		/// Parses the manifest yaml at most once per edit (DrawReferenceImages runs
		/// every OnGUI event). Returns false with <see cref="_cachedReferenceManifestError"/>
		/// set when the text doesn't parse.
		/// </summary>
		private bool TryGetCachedReferenceManifest(out SetManifest manifest)
		{
			if (_cachedReferenceManifestSource != _manifestYaml)
			{
				_cachedReferenceManifestSource = _manifestYaml;
				try
				{
					_cachedReferenceManifest = ManifestYaml.Read(_manifestYaml);
					_cachedReferenceManifestError = null;
				}
				catch (Exception ex)
				{
					_cachedReferenceManifest = null;
					_cachedReferenceManifestError = ex.Message;
				}
			}

			manifest = _cachedReferenceManifest ?? new SetManifest();
			return _cachedReferenceManifest != null;
		}

		/// <summary>Scans the image folder at most once per folder change (or per OnFocus); avoids per-frame disk I/O.</summary>
		private IReadOnlyList<string> CachedImageFiles()
		{
			if (_cachedImageFilesFolder != _imageFolder)
			{
				_cachedImageFilesFolder = _imageFolder;
				_cachedImageFiles = EnumerateImageFiles(_imageFolder);
			}

			return _cachedImageFiles;
		}

		private static IReadOnlyList<string> EnumerateImageFiles(string folder)
		{
			var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
			return Directory.GetFiles(folder)
				.Where(f => extensions.Contains(Path.GetExtension(f)))
				.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		/// <summary>Loads (and caches) a thumbnail for a reference file, rebuilding the cache when the folder changes.</summary>
		private Texture2D? ThumbnailFor(string path)
		{
			if (_thumbnailFolder != _imageFolder)
			{
				DisposeThumbnails();
				_thumbnailFolder = _imageFolder;
			}

			if (_referenceThumbnails.TryGetValue(path, out var cached))
			{
				return cached;
			}

			Texture2D? texture = null;
			try
			{
				var candidate = new Texture2D(2, 2);
				if (candidate.LoadImage(File.ReadAllBytes(path)))
				{
					texture = candidate;
				}
				else
				{
					DestroyImmediate(candidate);
				}
			}
			catch (Exception)
			{
				texture = null;
			}

			_referenceThumbnails[path] = texture;
			return texture;
		}

		private void DisposeThumbnails()
		{
			foreach (var texture in _referenceThumbnails.Values)
			{
				if (texture != null)
				{
					DestroyImmediate(texture);
				}
			}

			_referenceThumbnails.Clear();
		}

		private void DrawRunControls()
		{
			EditorGUILayout.BeginHorizontal();
			using (new EditorGUI.DisabledScope(_isRunning || (!_usePlan && string.IsNullOrWhiteSpace(_apiKey)) || string.IsNullOrWhiteSpace(_manifestYaml)))
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

			// Originals and their revisions ("{baseId}-v{n}") group together in order,
			// so a refined model reads as sitting next to the one it came from.
			foreach (var result in OrderedResults())
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

		/// <summary>Gallery order: by base asset, then revision number, so each
		/// original is immediately followed by its revisions in sequence.</summary>
		private IEnumerable<ModelResult> OrderedResults() =>
			_results.Values
				.OrderBy(r => BaseIdOf(r.AssetId), StringComparer.Ordinal)
				.ThenBy(r => RevisionOf(r.AssetId, BaseIdOf(r.AssetId)) ?? 0);

		/// <summary>The base manifest asset id behind a gallery slot — the slot
		/// itself for originals, the mapped base for "{baseId}-v{n}" revisions.</summary>
		private string BaseIdOf(string slot) => _baseAssetId.GetValueOrDefault(slot, slot);

		/// <summary>The next free revision slot for a base asset: the original counts
		/// as v1, so the first refinement is "-v2", the next "-v3", and so on across
		/// every existing slot for that base.</summary>
		private string NextRevisionId(string baseId)
		{
			var highest = _results.Keys
				.Select(slot => RevisionOf(slot, baseId))
				.Where(v => v.HasValue)
				.Select(v => v!.Value)
				.DefaultIfEmpty(1)
				.Max();
			return $"{baseId}-v{highest + 1}";
		}

		/// <summary>The revision number a slot represents for a given base — 1 for the
		/// bare base id, n for "{baseId}-v{n}", null if the slot isn't that base.</summary>
		private static int? RevisionOf(string slot, string baseId)
		{
			if (slot == baseId)
			{
				return 1;
			}

			var prefix = baseId + "-v";
			return slot.StartsWith(prefix, StringComparison.Ordinal)
				&& int.TryParse(slot[prefix.Length..], out var version)
					? version
					: null;
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
				DrawPalette(assembled.Model.Palette);
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
					RunSingleAssetAsync(result.AssetId, note);
				}
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndVertical();
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndVertical();
		}

		/// <summary>
		/// A row of colour swatches for the model's palette, under the voxel size
		/// info. Each swatch carries its key + hex as a hover tooltip; the row wraps
		/// once it fills the card so large palettes stay visible.
		/// </summary>
		private static void DrawPalette(IReadOnlyList<PaletteEntry> palette)
		{
			if (palette.Count == 0)
			{
				return;
			}

			const float swatch = 14f;
			const int perRow = 14;
			for (var start = 0; start < palette.Count; start += perRow)
			{
				EditorGUILayout.BeginHorizontal();
				for (var i = start; i < Math.Min(start + perRow, palette.Count); i++)
				{
					var entry = palette[i];
					var rect = GUILayoutUtility.GetRect(swatch, swatch, GUILayout.Width(swatch), GUILayout.Height(swatch));
					EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.4f));
					EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), entry.Colour);
					GUI.Label(rect, new GUIContent(string.Empty, $"{entry.Key}: {entry.ToHex()}"));
				}

				GUILayout.FlexibleSpace();
				EditorGUILayout.EndHorizontal();
			}
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

			EditorGUILayout.LabelField(
				_usePlan
					? $"API-equivalent cost saved (billed to plan): ~${totalUsd:0.000}"
					: $"Estimated spend: ~${totalUsd:0.000}",
				EditorStyles.boldLabel);
		}

		private void DrawLog()
		{
			var active = _showVerbose ? _verboseLog : _log;

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_showVerbose = GUILayout.Toggle(_showVerbose, new GUIContent(
					"Verbose",
					"Show every LLM call's full prompt, response, tool calls and token usage interleaved " +
					"with the headlines. Both streams are recorded on every run, so this switches the view " +
					"after the fact — no need to arm it beforehand."), GUILayout.Width(70));
				if (scope.changed)
				{
					EditorPrefs.SetBool(VerboseLogPref, _showVerbose);
					// The two streams differ in length, so the cached text/height must
					// be rebuilt for whichever one is now showing.
					_logDirty = true;
					Repaint();
				}
			}

			using (new EditorGUI.DisabledScope(_log.Length == 0 && _verboseLog.Length == 0))
			{
				if (GUILayout.Button("Clear", GUILayout.Width(50)))
				{
					_log.Clear();
					_verboseLog.Clear();
					_logDirty = true;
				}
			}

			EditorGUILayout.EndHorizontal();
			if (active.Length == 0)
			{
				return;
			}

			_logStyle ??= new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = 10 };

			// Only rebuild the string when the active log actually changed —
			// otherwise this runs every repaint, and verbose logs make that O(MB).
			// Invalidated on append (Log/LogTranscript) and when the toggle flips
			// which stream is shown.
			if (_logDirty)
			{
				_logText = active.ToString();
				_logDirty = false;
				_logMeasuredWidth = -1f;
			}

			// Re-measure (but don't rebuild the text) when the sidebar is dragged, so
			// the wrapped height tracks the new width without clipping. CalcHeight is
			// far cheaper than the ToString above, so this is fine per drag frame.
			if (!Mathf.Approximately(_logMeasuredWidth, _sidebarWidth))
			{
				_logHeight = _logStyle.CalcHeight(new GUIContent(_logText), _sidebarWidth - 40f);
				_logMeasuredWidth = _sidebarWidth;
			}

			_logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.ExpandHeight(true));
			EditorGUILayout.SelectableLabel(_logText, _logStyle, GUILayout.Height(Mathf.Max(_logHeight, 60f)), GUILayout.ExpandWidth(true));
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
				Log("Manifest generated. Review it, assign reference images below if you have them, then run the batch.");
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
				// Up-front precheck: warn about any references: files that don't exist
				// on disk before spending a run (mirrors the validate-* prechecks).
				var missing = await SetOrchestrator.MissingReferencesAsync(manifest, BuildImageSource(), _cts!.Token);
				if (missing.Count > 0)
				{
					Log("WARNING: these reference files are missing and will fail their assets:\n  " +
						string.Join("\n  ", missing));
				}

				using var gateway = NewGateway();

				// Name the run folder before any asset finishes — exports use
				// _runFolder, so the descriptive name must be settled up front.
				await NameRunFolderAsync(gateway, manifest);

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

		private async void RunSingleAssetAsync(string slotId, string refinementNote)
		{
			if (!TryParseManifest(out var manifest))
			{
				return;
			}

			// The clicked card may be a revision ("{baseId}-v{n}"); the orchestrator
			// always works against the underlying manifest asset.
			var baseId = BaseIdOf(slotId);
			var asset = manifest.Assets.FirstOrDefault(a => a.Id == baseId);
			if (asset == null)
			{
				Log($"{baseId}: not present in the current manifest.");
				return;
			}

			// Single-asset runs reuse the existing run folder (the revision exports
			// next to the original) and keep the log. Progress lines are keyed by the
			// base id, so the in-flight overlay rides on the base id too.
			StartRun(clearResults: false, newRunFolder: _runFolder.Length == 0);
			_inFlight[baseId] = "queued...";
			try
			{
				using var gateway = NewGateway();
				var orchestrator = NewOrchestrator(gateway);

				// A non-empty note over a usable previous result refines (minimal
				// edits) instead of re-planning from scratch; everything else (the
				// Regenerate button, or a refine with no good base) runs the full pipeline.
				var canRefine = refinementNote.Length > 0
					&& _results.TryGetValue(slotId, out var previous)
					&& previous.Status != ModelStatus.Failed
					&& previous.Model.Parts.Count > 0;
				var result = canRefine
					? await orchestrator.RefineAssetAsync(manifest, asset, _results[slotId], refinementNote, _cts!.Token, NewProgress())
					: await orchestrator.RunAssetAsync(manifest, asset, refinementNote, _cts!.Token, NewProgress());

				// A successful refine becomes a brand-new revision slot beside the
				// original; a regenerate or a failed refine acts on the clicked slot
				// in place (so the error lands on the card the operator touched).
				var targetSlot = canRefine && result.Status != ModelStatus.Failed
					? NextRevisionId(baseId)
					: slotId;
				if (targetSlot != baseId)
				{
					_baseAssetId[targetSlot] = baseId;
				}

				StoreResult(result with { AssetId = targetSlot }, inFlightKey: baseId);
			}
			catch (OperationCanceledException)
			{
				Log("Cancelled.");
			}
			catch (Exception ex)
			{
				Log($"{baseId} failed: " + ex.Message);
			}
			finally
			{
				FinishRun();
			}
		}

		// Every call's full prompt/response/tool-use/usage is always captured into the
		// verbose stream (the operator decides later whether to view it). Progress<string>
		// marshals each transcript to the main thread, so the StringBuilders are only ever
		// touched there even though assets run concurrently on background threads. On the
		// plan the same transport seam is the CLI gateway, which bills the subscription and
		// needs no API key; otherwise the API-backed gateway as before.
		private IAnthropicGateway NewGateway() =>
			_usePlan
				? new ClaudeCliGateway(_usage,
					onActivity: status => _statusLine = status,
					onTranscript: new Progress<string>(LogTranscript))
				: new AnthropicGateway(_apiKey, _usage,
					onActivity: status => _statusLine = status,
					onTranscript: new Progress<string>(LogTranscript));

		private SetOrchestrator NewOrchestrator(IAnthropicGateway gateway)
		{
			var config = BuildConfig();
			var runner = new ExecutorPartScriptRunner(new VoxelScriptExecutor(config.ScriptLimits));
			return new SetOrchestrator(gateway, config, BuildImageSource(), runner, _usage);
		}

		private IReferenceImageSource BuildImageSource() =>
			string.IsNullOrWhiteSpace(_imageFolder)
				? NullReferenceImageSource.Instance
				: new FileReferenceImageSource(_imageFolder);

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

		private void StoreResult(ModelResult result, string? inFlightKey = null)
		{
			// The in-flight overlay is keyed by the base id during a refine, which
			// differs from the new revision's slot id — clear by that key.
			_inFlight.Remove(inFlightKey ?? result.AssetId);

			// A failed refine/regenerate must not destroy a good previous result:
			// keep the old export/preview and surface the error on it instead.
			if (result.Status == ModelStatus.Failed &&
				_results.TryGetValue(result.AssetId, out var existing) &&
				existing.Status != ModelStatus.Failed)
			{
				_results[result.AssetId] = existing with { Error = "refine failed: " + result.Error };
				Log($"{result.AssetId}: refine failed — kept the previous result. {result.Error}");
				return;
			}

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

		/// <summary>
		/// Replaces the timestamp-only run folder with "{timestamp}-{descriptive}",
		/// the descriptive tail generated from the manifest by the LLM. Best-effort:
		/// a naming failure leaves the timestamp fallback in place rather than
		/// aborting the batch; cancellation propagates so a cancelled run stops here.
		/// </summary>
		private async Task NameRunFolderAsync(IAnthropicGateway gateway, SetManifest manifest)
		{
			if (_runTimestamp.Length == 0)
			{
				return;
			}

			try
			{
				Log("Naming run folder...");
				var slug = await new RunFolderNamer(gateway, BuildConfig()).NameAsync(manifest, _cts!.Token);
				_runFolder = Path.Combine(_outputFolder, $"{_runTimestamp}-{slug}");
				Log($"Run folder: {_runFolder}");
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				Log("Run folder naming failed; keeping timestamp: " + ex.Message);
			}
		}

		private void StartRun(bool clearResults, bool newRunFolder = true)
		{
			_isRunning = true;
			_cts = new CancellationTokenSource();
			_statusLine = string.Empty;

			// Only a fresh batch wipes the log; a single regenerate/refine appends to
			// it so its history stays alongside the run that produced the asset.
			if (clearResults)
			{
				_log.Clear();
				_verboseLog.Clear();
				_logDirty = true;
			}

			_inFlight.Clear();
			if (newRunFolder)
			{
				// The descriptive tail is filled in asynchronously once a manifest
				// is in hand (NameRunFolderAsync); until then the timestamp alone is
				// the fallback so a run always has somewhere to land.
				_runTimestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
				_runFolder = Path.Combine(_outputFolder, $"run-{_runTimestamp}");
			}
			else
			{
				_runTimestamp = string.Empty;
				_runFolder = string.Empty;
			}
			_runTimer.Restart();
			if (clearResults)
			{
				_results.Clear();
				_baseAssetId.Clear();
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

			// Both streams are persisted: session.log is the headline narrative,
			// session.verbose.log adds every full LLM transcript inline.
			Directory.CreateDirectory(_runFolder);
			File.WriteAllText(Path.Combine(_runFolder, "session.log"), _log.ToString());
			File.WriteAllText(Path.Combine(_runFolder, "session.verbose.log"), _verboseLog.ToString());
			AssetDatabase.Refresh();
		}

		/// <summary>A headline: written to both the regular and the verbose stream so
		/// the verbose view stays a superset of the regular one.</summary>
		private void Log(string message)
		{
			var entry = FormatEntry(message);
			_log.Append(entry);
			_verboseLog.Append(entry);
			AfterAppend();
		}

		/// <summary>A full LLM transcript: written only to the verbose stream, so the
		/// regular view stays a readable headline narrative.</summary>
		private void LogTranscript(string message)
		{
			_verboseLog.Append(FormatEntry(message));
			AfterAppend();
		}

		// Each entry opens with a bullet + run timestamp so entries are easy to tell
		// apart in the stream.
		private string FormatEntry(string message)
		{
			var builder = new StringBuilder("• ");
			if (_runTimer.IsRunning)
			{
				builder.Append('[').Append(_runTimer.Elapsed.ToString(@"mm\:ss")).Append("] ");
			}

			return builder.AppendLine(message).ToString();
		}

		private void AfterAppend()
		{
			_logDirty = true;
			_logScroll.y = float.MaxValue;
			Repaint();
		}
	}
}
