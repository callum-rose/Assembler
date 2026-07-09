#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Assembler.AssetGeneration.EditorCommon;
using Assembler.AssetGeneration.ImageToMesh;
using Assembler.AssetGeneration.MeshToVoxel.Generation;
using Assembler.AssetGeneration.TextToImage;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.TextToVoxelPipeline.Editor
{
	/// <summary>
	/// Editor window for the full text → voxel pipeline: type a prompt and get a <c>.vox</c>,
	/// driving the shared <see cref="VoxelPipeline.RunAsync"/> so the window and any headless caller
	/// take an identical path. The gap between stages is optionally reviewable — tick "Review image"
	/// / "Review mesh" and the run pauses after that stage (showing the image preview / the mesh path)
	/// until you press Continue, Retry (re-run that stage), or Cancel, so you can sanity-check an
	/// intermediate before paying for the next stage. Stage 3 mirrors the standalone Mesh → Voxel
	/// Mesh to Voxel window's full control set. All inputs are persisted in <see cref="EditorPrefs"/>.
	/// </summary>
	public sealed class VoxelPipelineWindow : EditorWindow
	{
		private const string Pref = "Assembler.TextToVoxel.";

		// SessionState (survives a domain reload, wiped on editor restart) key for the in-progress-run
		// manifest that drives the "Resume run" button — see ResumeManifest / DrawResume.
		private const string ResumeKey = Pref + "ResumeManifest";

		// Canonical per-provider image-key id (shared with the standalone Text to Image window), plus the
		// legacy pref keys the key may still live under so an already-entered key isn't lost.
		private static string ImageKeyId(ImageProvider provider) => $"Image.{provider}";

		private static string[] LegacyImageKeys(ImageProvider provider) => new[]
		{
			$"Assembler.ImageGen.ApiKey.{provider}",
			$"Assembler.TextToVoxel.ImageApiKey.{provider}",
		};

		private readonly VoxelPipelineSettings _settings = new();

		// Stage 2 (image → mesh) and stage 3 (mesh → voxels) control sets, shared with the standalone
		// Image → Mesh and Mesh → Voxel windows. Assembled into _settings at run time.
		private MeshyRequest _meshy = MeshySettingsGui.Default();
		private readonly VoxSettingsGui _vox = new();

		private bool _reviewImage;
		private bool _reviewMesh;

		// AI-config import (paste JSON from the AI Model Config window).
		private bool _showImport;
		private string _aiConfigPaste = "";

		private bool _running;
		private string _status = "Idle.";
		private CancellationTokenSource? _cts;
		private Vector2 _scroll;
		private readonly TexturePreview _preview = new();

		// Review-gate state: while a stage is awaiting Continue, the window shows the intermediate.
		private enum ReviewStage { None, Image, Mesh }
		private ReviewStage _reviewStage = ReviewStage.None;
		private TaskCompletionSource<VoxelPipeline.ReviewDecision>? _reviewGate;
		private CancellationTokenRegistration _reviewRegistration;
		private string _reviewMeshPath = "";

		[MenuItem("Assembler/Voxelisation/Text to Voxels (pipeline)")]
		public static void Open()
		{
			var window = GetWindow<VoxelPipelineWindow>("Text to Voxels");
			window.minSize = new Vector2(480, 640);
		}

		private void OnEnable() => LoadState();

		private void OnDisable()
		{
			SaveState();
			_preview.Clear();
		}

		private void OnGUI()
		{
			_scroll = EditorGUILayout.BeginScrollView(_scroll);

			using (new EditorGUI.DisabledScope(_running))
			{
				DrawImportFromAi();
				EditorGUILayout.Space();
				DrawPromptStage();
				EditorGUILayout.Space();
				DrawMeshStage();
				EditorGUILayout.Space();
				DrawVoxelStage();
				EditorGUILayout.Space();
				DrawOutput();
				EditorGUILayout.Space();
				DrawReviewToggles();
			}

			EditorGUILayout.Space();
			DrawActions();
			EditorGUILayout.Space();
			EditorGUILayout.HelpBox(_status, _running ? MessageType.Info : MessageType.None);

			DrawReviewPanel();
			DrawPreview();

			EditorGUILayout.EndScrollView();
		}

		// ---- Import from the AI Model Config window --------------------------

		// Paste the config JSON the AI Model Config window produced and apply it to the run
		// (prompt + voxel settings + Meshy settings). A pasted value is parsed on demand — nothing is
		// shared between the two windows, so there are no fragile pref keys.
		private void DrawImportFromAi()
		{
			_showImport = EditorGUILayout.Foldout(_showImport, "Import AI config (paste JSON)", true);
			if (!_showImport)
			{
				return;
			}

			using (new EditorGUI.IndentLevelScope())
			{
				EditorGUILayout.LabelField(
					"Paste the config JSON from the AI Model Config window (the whole reply or just the json).",
					EditorStyles.miniLabel);
				var wrap = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
				_aiConfigPaste = EditorGUILayout.TextArea(_aiConfigPaste, wrap, GUILayout.MinHeight(70));
				using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_aiConfigPaste)))
				{
					if (GUILayout.Button("Import"))
					{
						ImportFromAi();
					}
				}
			}
		}

		private void ImportFromAi()
		{
			try
			{
				// Accept either a full fenced assistant reply or the bare json object.
				var text = _aiConfigPaste.Trim();
				var json = ConfigExtractor.Extract(text) ?? text;
				var config = ConfigParser.ParseJson(json);

				if (!string.IsNullOrEmpty(config.ImagePrompt))
				{
					_settings.Prompt = config.ImagePrompt;
				}

				if (!string.IsNullOrEmpty(config.BaseName))
				{
					_settings.BaseName = config.BaseName;
				}

				// Mesh → voxel settings map onto the stage-3 draft (the master palette is the window's,
				// never the AI's, so ApplyImported leaves it untouched).
				_vox.ApplyImported(config.Settings);

				// Image → mesh (Meshy) generation parameters map onto the stage-2 draft.
				var m = config.Meshy;
				_meshy.AiModel = m.MeshAiModel;
				_meshy.Format = m.MeshFormat;
				_meshy.GenerateTexture = m.GenerateTexture;
				_meshy.EnablePbr = m.EnablePbr;
				_meshy.HdTexture = m.HdTexture;
				_meshy.Remesh = m.Remesh;
				_meshy.Topology = m.Topology;
				_meshy.Decimation = m.Decimation;
				_meshy.TargetPolycount = m.TargetPolycount;
				_meshy.SavePreRemeshedModel = m.SavePreRemeshedModel;
				_meshy.RemoveLighting = m.RemoveLighting;
				_meshy.AutoSize = m.AutoSize;
				_meshy.OriginAt = m.OriginAt;
				_meshy.Moderation = m.Moderation;
				_meshy.MultiViewThumbnails = m.MultiViewThumbnails;
				_meshy.AlphaThumbnail = m.AlphaThumbnail;

				// Drop keyboard focus so the prompt text field repaints with the imported value.
				GUI.FocusControl(null);
				SaveState();
				SetStatus($"Imported AI config — {config.Settings.MaxDimVoxels} voxels.");
			}
			catch (Exception e)
			{
				SetStatus($"Import failed: {e.Message}");
			}
		}

		// ---- Stage 1: prompt → image ----------------------------------------

		private void DrawPromptStage()
		{
			EditorGUILayout.LabelField("1 · Text → Image", EditorStyles.boldLabel);

			EditorGUI.BeginChangeCheck();
			_settings.ImageProvider = (ImageProvider)EditorGUILayout.EnumPopup("Provider", _settings.ImageProvider);
			if (EditorGUI.EndChangeCheck())
			{
				_settings.ImageApiKey = ApiKeyStore.Load(ImageKeyId(_settings.ImageProvider), LegacyImageKeys(_settings.ImageProvider));
				_settings.ImageModel = ImageGeneratorFactory.DefaultModelFor(_settings.ImageProvider);
			}

			_settings.ImageModel = ModelPopup.Draw(
				"Image Model", _settings.ImageModel, ImageGeneratorFactory.AvailableModelsFor(_settings.ImageProvider));

			_settings.ImageApiKey = ApiKeyField.Draw("Image API Key", _settings.ImageApiKey,
				key => ApiKeyStore.Save(ImageKeyId(_settings.ImageProvider), key));

			EditorGUILayout.LabelField("Prompt");
			var wrap = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
			_settings.Prompt = EditorGUILayout.TextArea(_settings.Prompt, wrap, GUILayout.MinHeight(70));
		}

		// ---- Stage 2: image → mesh ------------------------------------------

		private void DrawMeshStage()
		{
			EditorGUILayout.LabelField("2 · Image → Mesh (Meshy.ai)", EditorStyles.boldLabel);

			_settings.MeshyApiKey = ApiKeyField.Draw("Meshy API Key", _settings.MeshyApiKey,
				key => ApiKeyStore.Save("Meshy", key));

			MeshySettingsGui.Draw(ref _meshy);
		}

		// ---- Stage 3: mesh → voxels (the full Mesh → Voxel control set) ----

		private void DrawVoxelStage()
		{
			EditorGUILayout.LabelField("3 · Mesh → Voxels", EditorStyles.boldLabel);
			EditorGUILayout.LabelField(
				"Voxelisation runs synchronously — the editor blocks while stage 3 runs.", EditorStyles.miniLabel);

			EditorGUILayout.Space();
			_vox.Draw();
		}

		// ---- Output + review toggles ----------------------------------------

		private void DrawOutput()
		{
			EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
			using (new EditorGUILayout.HorizontalScope())
			{
				_settings.OutputDir = EditorGUILayout.TextField("Output Directory", _settings.OutputDir);
				if (GUILayout.Button("Browse", GUILayout.Width(70)))
				{
					var picked = EditorUtility.OpenFolderPanel("Output directory", PathField.GuessStartDir(_settings.OutputDir), "");
					if (!string.IsNullOrEmpty(picked))
					{
						_settings.OutputDir = picked;
					}
				}
			}

			_settings.OutputDir = PathField.HandleDrop(GUILayoutUtility.GetLastRect(), _settings.OutputDir, wantFolder: true);
			_settings.BaseName = EditorGUILayout.TextField(
				new GUIContent("Base Name", "Shared by all three files (image/mesh/.vox). Leave blank to slug it from the prompt."),
				_settings.BaseName);
			_settings.AutoSubfolderPerRun = EditorGUILayout.ToggleLeft(
				new GUIContent("Subfolder per run",
					"Put each run's files in their own <base>_<timestamp> subfolder so runs don't overwrite each other. Off: write straight into the output directory."),
				_settings.AutoSubfolderPerRun);

			var baseName = VoxelPipeline.ResolveBaseName(_settings);
			var prefix = _settings.AutoSubfolderPerRun ? $"{baseName}_<timestamp>/" : "";
			EditorGUILayout.LabelField(" ", $"→ {prefix}{baseName}.png / .obj / .vox", EditorStyles.miniLabel);
		}

		private void DrawReviewToggles()
		{
			EditorGUILayout.LabelField("Review gates", EditorStyles.boldLabel);
			_reviewImage = EditorGUILayout.ToggleLeft(
				new GUIContent("Review image before meshing", "Pause after stage 1 to inspect the generated image."), _reviewImage);
			_reviewMesh = EditorGUILayout.ToggleLeft(
				new GUIContent("Review mesh before voxelizing", "Pause after stage 2 to inspect the generated mesh."), _reviewMesh);
		}

		private void DrawActions()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				using (new EditorGUI.DisabledScope(_running))
				{
					if (GUILayout.Button("Run pipeline", GUILayout.Height(30)))
					{
						_ = RunAsync(resume: false);
					}
				}
				using (new EditorGUI.DisabledScope(!_running))
				{
					if (GUILayout.Button("Cancel", GUILayout.Height(30), GUILayout.Width(100)))
					{
						_cts?.Cancel();
					}
				}
			}

			DrawResume();
		}

		// A domain reload (script edit, entering Play mode) mid-run tears down the AppDomain and silently
		// kills the fire-and-forget run — but the run's output folder is pinned in a SessionState manifest
		// that survives the reload. If one is present and nothing is running, offer to resume: re-invoke
		// the unchanged pipeline reusing that folder, so stage 2's paid Meshy task resumes from its
		// .meshy-task-id sidecar (the only irreversible cost) while stages 1/3 re-run locally.
		private void DrawResume()
		{
			if (_running || !TryLoadResumeManifest(out var manifest))
			{
				return;
			}

			EditorGUILayout.Space();
			EditorGUILayout.HelpBox(
				$"A previous run was interrupted (domain reload) with partial output in\n{manifest.ResolvedDir}\n\n"
				+ "Resume re-runs the pipeline reusing that folder: the paid Meshy task resumes from its "
				+ "sidecar, while the image is regenerated and the mesh re-voxelised. Review gates are skipped.",
				MessageType.Warning);
			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Resume run", GUILayout.Height(26)))
				{
					_ = RunAsync(resume: true);
				}

				if (GUILayout.Button(new GUIContent("Discard", "Forget the interrupted run without resuming."),
						GUILayout.Height(26), GUILayout.Width(90)))
				{
					ClearResumeManifest();
					SetStatus("Discarded the interrupted run.");
				}
			}
		}

		// ---- Review panel (shown while a stage awaits Continue) --------------

		private void DrawReviewPanel()
		{
			if (_reviewStage == ReviewStage.None)
			{
				return;
			}

			EditorGUILayout.Space();
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				var what = _reviewStage == ReviewStage.Image ? "image" : "mesh";
				EditorGUILayout.LabelField($"Review the {what}", EditorStyles.boldLabel);

				if (_reviewStage == ReviewStage.Mesh)
				{
					EditorGUILayout.SelectableLabel(_reviewMeshPath, EditorStyles.textField,
						GUILayout.Height(EditorGUIUtility.singleLineHeight));
					if (AssetPaths.IsUnderAssets(_reviewMeshPath) && GUILayout.Button("Select in Project", GUILayout.Width(140)))
					{
						PingAsset(_reviewMeshPath);
					}
				}

				using (new EditorGUILayout.HorizontalScope())
				{
					if (GUILayout.Button("Continue ▶", GUILayout.Height(26)))
					{
						ResolveReview(VoxelPipeline.ReviewDecision.Continue);
					}

					if (GUILayout.Button(new GUIContent($"↻ Retry {what}", $"Discard this {what} and run the stage again."),
							GUILayout.Height(26), GUILayout.Width(120)))
					{
						ResolveReview(VoxelPipeline.ReviewDecision.Retry);
					}

					if (GUILayout.Button("Cancel", GUILayout.Height(26), GUILayout.Width(100)))
					{
						_cts?.Cancel();
					}
				}
			}
		}

		private void DrawPreview()
		{
			if (!_preview.HasTexture)
			{
				return;
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Image preview", EditorStyles.boldLabel);
			_preview.Draw(position.width - 30);
		}

		// ---- Run -------------------------------------------------------------

		// A single run. resume=false is a fresh Run-pipeline press; resume=true re-kicks an interrupted run
		// from its SessionState manifest (see DrawResume). The output folder is pinned for the run so a
		// resume reuses the same directory (and therefore the same .meshy-task-id sidecar): ResolveOutputDir
		// uses DateTime.Now, so a naive re-run would otherwise compute a *different* timestamped folder and
		// miss the partials. The pin is in-memory only and restored in finally; SaveState() persists the
		// user's original OutputDir/BaseName first, so a mid-run domain reload leaves prefs untouched.
		private async Task RunAsync(bool resume)
		{
			SaveState();
			ApplyMeshyToSettings();
			_settings.Vox = _vox.ToSettings();

			var originalOutputDir = _settings.OutputDir;
			var originalBaseName = _settings.BaseName;
			var originalAutoSubfolder = _settings.AutoSubfolderPerRun;

			string resolvedDir;
			string baseName;
			if (resume && TryLoadResumeManifest(out var manifest))
			{
				resolvedDir = manifest.ResolvedDir;
				baseName = manifest.BaseName;
			}
			else
			{
				baseName = VoxelPipeline.ResolveBaseName(_settings);
				resolvedDir = VoxelPipeline.ResolveOutputDir(_settings, baseName, DateTime.Now);
			}

			// Pin the resolved folder + base name so VoxelPipeline.RunAsync writes exactly here on a resume.
			_settings.OutputDir = resolvedDir;
			_settings.BaseName = baseName;
			_settings.AutoSubfolderPerRun = false;
			SaveResumeManifest(new ResumeManifest { ResolvedDir = resolvedDir, BaseName = baseName });

			_running = true;
			_cts = new CancellationTokenSource();
			var ct = _cts.Token;

			// On resume the interactive review gates can't be restored across the reload, so skip them and
			// run straight through (the earlier stages just re-produce their outputs).
			VoxelPipeline.ReviewGate<ImageGenerationCore.Result>? imageGate = !resume && _reviewImage ? ImageReviewGate : null;
			VoxelPipeline.ReviewGate<MeshyConversionCore.Result>? meshGate = !resume && _reviewMesh ? MeshReviewGate : null;

			try
			{
				var result = await VoxelPipeline.RunAsync(
					_settings, ct, SetStatus,
					reviewImage: imageGate,
					reviewMesh: meshGate,
					voxelProgress: (fraction, stage) =>
						EditorUtility.DisplayProgressBar("Mesh → VOX", $"{stage}…", fraction));

				// The pipeline reports every outcome through the result. On success the in-run "Done."
				// status already stands; otherwise surface the case's message, and for a per-stage
				// failure also dump the carried exception's stack.
				var stageError = result switch
				{
					VoxelPipeline.Result.ImageFailed f => f.Error,
					VoxelPipeline.Result.MeshFailed f => f.Error,
					VoxelPipeline.Result.VoxelizationFailed f => f.Error,
					_ => null,
				};

				if (result is not VoxelPipeline.Result.Success)
				{
					SetStatus(result.ToString());
				}

				if (stageError is not null)
				{
					Debug.LogException(stageError);
				}
			}
			catch (Exception e)
			{
				SetStatus($"Error: {e.Message}");
				Debug.LogException(e);
			}
			finally
			{
				EditorUtility.ClearProgressBar();
				EndReview();
				// The run finished (or errored/cancelled) without a reload, so there is nothing to resume.
				// A domain reload mid-run never reaches here, leaving the manifest in place for DrawResume.
				ClearResumeManifest();
				_settings.OutputDir = originalOutputDir;
				_settings.BaseName = originalBaseName;
				_settings.AutoSubfolderPerRun = originalAutoSubfolder;
				_running = false;
				_cts?.Dispose();
				_cts = null;
				Repaint();
			}
		}

		// ---- Resume manifest (SessionState, survives a domain reload) --------

		[Serializable]
		private struct ResumeManifest
		{
			public string ResolvedDir;
			public string BaseName;
		}

		private static void SaveResumeManifest(ResumeManifest manifest) =>
			SessionState.SetString(ResumeKey, JsonUtility.ToJson(manifest));

		private static void ClearResumeManifest() => SessionState.EraseString(ResumeKey);

		private static bool TryLoadResumeManifest(out ResumeManifest manifest)
		{
			var json = SessionState.GetString(ResumeKey, "");
			if (string.IsNullOrEmpty(json))
			{
				manifest = default;
				return false;
			}

			manifest = JsonUtility.FromJson<ResumeManifest>(json);
			return !string.IsNullOrEmpty(manifest.ResolvedDir);
		}

		private Task<VoxelPipeline.ReviewDecision> ImageReviewGate(ImageGenerationCore.Result image, CancellationToken ct)
		{
			LoadPreview(image.Image.Bytes);
			return BeginReview(ReviewStage.Image, ct);
		}

		private Task<VoxelPipeline.ReviewDecision> MeshReviewGate(MeshyConversionCore.Result mesh, CancellationToken ct)
		{
			_reviewMeshPath = mesh.OutputPath;
			return BeginReview(ReviewStage.Mesh, ct);
		}

		// Hand control to the user: park on a TaskCompletionSource the Continue/Retry buttons complete
		// (Cancel cancels the run's token, which fails the gate the same way as throwing would).
		private Task<VoxelPipeline.ReviewDecision> BeginReview(ReviewStage stage, CancellationToken ct)
		{
			_reviewStage = stage;
			_reviewGate = new TaskCompletionSource<VoxelPipeline.ReviewDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
			_reviewRegistration = ct.Register(() => _reviewGate?.TrySetCanceled(ct));
			Repaint();
			return _reviewGate.Task;
		}

		private void ResolveReview(VoxelPipeline.ReviewDecision decision)
		{
			_reviewRegistration.Dispose();
			_reviewStage = ReviewStage.None;
			var gate = _reviewGate;
			_reviewGate = null;
			gate?.TrySetResult(decision);
			Repaint();
		}

		private void EndReview()
		{
			_reviewRegistration.Dispose();
			_reviewStage = ReviewStage.None;
			_reviewGate = null;
		}

		private void LoadPreview(byte[] bytes) => _preview.Load(bytes);

		private void SetStatus(string message)
		{
			_status = message;
			Repaint();
		}

		// ---- Helpers ---------------------------------------------------------

		// Copy the stage-2 (Meshy) draft into the pipeline settings the run consumes.
		private void ApplyMeshyToSettings()
		{
			_settings.MeshFormat = _meshy.Format;
			_settings.MeshAiModel = _meshy.AiModel;
			_settings.GenerateTexture = _meshy.GenerateTexture;
			_settings.EnablePbr = _meshy.EnablePbr;
			_settings.HdTexture = _meshy.HdTexture;
			_settings.Remesh = _meshy.Remesh;
			_settings.Topology = _meshy.Topology;
			_settings.Decimation = _meshy.Decimation;
			_settings.TargetPolycount = _meshy.TargetPolycount;
			_settings.SavePreRemeshedModel = _meshy.SavePreRemeshedModel;
			_settings.RemoveLighting = _meshy.RemoveLighting;
			_settings.Moderation = _meshy.Moderation;
			_settings.AutoSize = _meshy.AutoSize;
			_settings.OriginAt = _meshy.OriginAt;
			_settings.MultiViewThumbnails = _meshy.MultiViewThumbnails;
			_settings.AlphaThumbnail = _meshy.AlphaThumbnail;
		}

		private static void PingAsset(string path)
		{
			var rel = AssetPaths.ToAssetRelative(path);
			if (rel == null)
			{
				return;
			}

			var obj = AssetDatabase.LoadMainAssetAtPath(rel);
			if (obj != null)
			{
				EditorGUIUtility.PingObject(obj);
				Selection.activeObject = obj;
			}
		}

		// ---- EditorPrefs persistence ----------------------------------------

		private void LoadState()
		{
			_settings.ImageProvider = (ImageProvider)EditorPrefs.GetInt(Pref + "Provider", (int)ImageProvider.GoogleGemini);
			_settings.ImageModel = EditorPrefs.GetString(Pref + "ImageModel." + _settings.ImageProvider, ImageGeneratorFactory.DefaultModelFor(_settings.ImageProvider));
			_settings.ImageApiKey = ApiKeyStore.Load(ImageKeyId(_settings.ImageProvider), LegacyImageKeys(_settings.ImageProvider));
			_settings.Prompt = EditorPrefs.GetString(Pref + "Prompt", "");

			_settings.MeshyApiKey = ApiKeyStore.Load("Meshy", "Meshy.ImageTo3D.ApiKey", "Assembler.TextToVoxel.MeshyApiKey");
			_meshy = MeshySettingsGui.Load(Pref + "Meshy.");
			_vox.Load(Pref + "Vox");

			_settings.OutputDir = EditorPrefs.GetString(Pref + "OutputDir", "Assets/TextToVoxel");
			_settings.BaseName = EditorPrefs.GetString(Pref + "BaseName", "");
			_settings.AutoSubfolderPerRun = EditorPrefs.GetBool(Pref + "AutoSubfolderPerRun", true);
			_reviewImage = EditorPrefs.GetBool(Pref + "ReviewImage", false);
			_reviewMesh = EditorPrefs.GetBool(Pref + "ReviewMesh", false);
		}

		private void SaveState()
		{
			EditorPrefs.SetInt(Pref + "Provider", (int)_settings.ImageProvider);
			EditorPrefs.SetString(Pref + "ImageModel." + _settings.ImageProvider, _settings.ImageModel);
			ApiKeyStore.Save(ImageKeyId(_settings.ImageProvider), _settings.ImageApiKey);
			EditorPrefs.SetString(Pref + "Prompt", _settings.Prompt);

			ApiKeyStore.Save("Meshy", _settings.MeshyApiKey);
			MeshySettingsGui.Save(Pref + "Meshy.", _meshy);
			_vox.Save(Pref + "Vox");

			EditorPrefs.SetString(Pref + "OutputDir", _settings.OutputDir);
			EditorPrefs.SetString(Pref + "BaseName", _settings.BaseName);
			EditorPrefs.SetBool(Pref + "AutoSubfolderPerRun", _settings.AutoSubfolderPerRun);
			EditorPrefs.SetBool(Pref + "ReviewImage", _reviewImage);
			EditorPrefs.SetBool(Pref + "ReviewMesh", _reviewMesh);
		}
	}
}
