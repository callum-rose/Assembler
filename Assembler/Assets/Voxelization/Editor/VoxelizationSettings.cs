using UnityEditor;
using UnityEngine;

namespace Assembler.Voxelization.Editor
{
	/// <summary>
	/// Persistent operator settings for the voxel pipeline, edited from
	/// <see cref="VoxelSetReviewWindow"/> and projected to a
	/// <see cref="VoxelizationConfig"/> for each run. Lives as a single asset so
	/// the stage models, granular retry/budget knobs and style guidance survive
	/// editor restarts (and are version-controllable / shareable) instead of
	/// hiding in per-machine EditorPrefs. Defaults mirror
	/// <see cref="VoxelizationConfig.Default"/>.
	/// </summary>
	public sealed class VoxelizationSettings : ScriptableObject
	{
		private const string AssetPath = "Assets/Voxelization/Editor/VoxelizationSettings.asset";

		[Header("Stage models")]
		public string ManifestModel = VoxelizationConfig.DefaultModel;
		public string PlanningModel = VoxelizationConfig.DefaultModel;
		public string AuthoringModel = VoxelizationConfig.DefaultModel;

		[Header("Style guidance (injected into planning, authoring and review)")]
		[TextArea(3, 8)]
		public string StyleGuidance = string.Empty;

		[Header("Reference brief extraction")]
		[Tooltip("Read the silhouette + palette deterministically from the reference pixels instead of via a vision call.")]
		public bool DeterministicBrief = VoxelizationConfig.Default.DeterministicBrief;

		[Tooltip("When deterministic, also make one slim vision call for the advisory proportions / signature features.")]
		public bool ExtractSemanticBriefFields = VoxelizationConfig.Default.ExtractSemanticBriefFields;

		[Header("Retry / budget knobs")]
		[Tooltip("Attempts per part inside one authoring call (initial + parse-failure retries).")]
		[Min(1)]
		public int MaxPartAttempts = VoxelizationConfig.Default.MaxPartAttempts;

		[Tooltip("Validation-driven re-author rounds per model after the first assembly.")]
		[Min(0)]
		public int MaxValidationRounds = VoxelizationConfig.Default.MaxValidationRounds;

		[Tooltip("Post-validation vision review rounds whose corrections trigger a full re-plan.")]
		[Min(0)]
		public int MaxReviewRounds = VoxelizationConfig.Default.MaxReviewRounds;

		[Tooltip("Declared-volume cap above which a planned layers part is demoted to a script part.")]
		[Min(1)]
		public int PartVoxelBudget = VoxelizationConfig.Default.PartVoxelBudget;

		[Tooltip("Brief silhouette IoU below which validation flags the built model.")]
		[Range(0f, 1f)]
		public float SilhouetteIouThreshold = VoxelizationConfig.Default.SilhouetteIouThreshold;

		[Tooltip("Fraction of reference-silhouette cells the planned part boxes must be able to reach.")]
		[Range(0f, 1f)]
		public float SilhouetteCoverageThreshold = VoxelizationConfig.Default.SilhouetteCoverageThreshold;

		[Header("Reference hull clip")]
		[Tooltip("Trim authored geometry that overhangs the reference silhouette before composition (per-run A/B).")]
		public bool EnableHullClip = VoxelizationConfig.Default.EnableHullClip;

		[Tooltip("Silhouette-mask dilation in cells; matches the ±1 planner width slack.")]
		[Min(0)]
		public int HullClipDilation = VoxelizationConfig.Default.HullClipDilation;

		[Tooltip("Per-part removed fraction at/above which a clip is a moderate trim (suggests a reposition).")]
		[Range(0f, 1f)]
		public float HullClipModerateRatio = VoxelizationConfig.Default.HullClipModerateRatio;

		[Tooltip("Per-part removed fraction at/above which a clip is refused (severe — keep authored, re-plan).")]
		[Range(0f, 1f)]
		public float HullClipSevereRatio = VoxelizationConfig.Default.HullClipSevereRatio;

		[Tooltip("Discard the whole hull when the aggregate removed-mass fraction exceeds this (bad reference).")]
		[Range(0f, 1f)]
		public float HullClipGlobalFloor = VoxelizationConfig.Default.HullClipGlobalFloor;

		/// <summary>
		/// Loads the shared settings asset, creating it (and migrating any values
		/// previously kept in EditorPrefs) the first time the window runs.
		/// </summary>
		public static VoxelizationSettings LoadOrCreate()
		{
			var settings = AssetDatabase.LoadAssetAtPath<VoxelizationSettings>(AssetPath);
			if (settings != null)
			{
				return settings;
			}

			settings = CreateInstance<VoxelizationSettings>();
			MigrateFromEditorPrefs(settings);
			AssetDatabase.CreateAsset(settings, AssetPath);
			AssetDatabase.SaveAssets();
			return settings;
		}

		/// <summary>Projects the persisted settings onto a run config.</summary>
		public VoxelizationConfig ToConfig() => VoxelizationConfig.Default with
		{
			ManifestModel = ManifestModel,
			PlanningModel = PlanningModel,
			AuthoringModel = AuthoringModel,
			StyleGuidance = StyleGuidance.Trim(),
			DeterministicBrief = DeterministicBrief,
			ExtractSemanticBriefFields = ExtractSemanticBriefFields,
			MaxPartAttempts = MaxPartAttempts,
			MaxValidationRounds = MaxValidationRounds,
			MaxReviewRounds = MaxReviewRounds,
			PartVoxelBudget = PartVoxelBudget,
			SilhouetteIouThreshold = SilhouetteIouThreshold,
			SilhouetteCoverageThreshold = SilhouetteCoverageThreshold,
			EnableHullClip = EnableHullClip,
			HullClipDilation = HullClipDilation,
			HullClipModerateRatio = HullClipModerateRatio,
			HullClipSevereRatio = HullClipSevereRatio,
			HullClipGlobalFloor = HullClipGlobalFloor,
		};

		// One-time carry-over so an operator who tuned the old EditorPrefs-backed
		// models / style guidance doesn't silently lose them on upgrade.
		private static void MigrateFromEditorPrefs(VoxelizationSettings settings)
		{
			const string stagePrefix = "Assembler.Voxelization.Model.";
			settings.StyleGuidance = EditorPrefs.GetString("Assembler.Voxelization.StyleGuidance", settings.StyleGuidance);
			settings.ManifestModel = EditorPrefs.GetString(stagePrefix + "Manifest", settings.ManifestModel);
			settings.PlanningModel = EditorPrefs.GetString(stagePrefix + "Planning", settings.PlanningModel);
			settings.AuthoringModel = EditorPrefs.GetString(stagePrefix + "Authoring", settings.AuthoringModel);
		}
	}
}
