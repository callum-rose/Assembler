using Assembler.Voxels.Scripting;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Knobs for the voxelization pipeline. Model ids are configurable per
	/// stage (Decision 10) and default to Sonnet everywhere; retry caps bound
	/// the validation loop so a stubborn part degrades to a gallery flag
	/// rather than an unbounded spend.
	/// </summary>
	public sealed record VoxelizationConfig
	{
		public const string DefaultModel = "claude-sonnet-4-6";

		public string ManifestModel { get; init; } = DefaultModel;
		public string PlanningModel { get; init; } = DefaultModel;
		public string AuthoringModel { get; init; } = DefaultModel;

		/// <summary>Attempts per part inside one authoring call (initial + parse-failure retries).</summary>
		public int MaxPartAttempts { get; init; } = 3;

		/// <summary>Validation-driven re-author rounds per model after the first assembly.</summary>
		public int MaxValidationRounds { get; init; } = 2;

		/// <summary>
		/// Post-validation review rounds: a vision-capable call compares the
		/// built views (plus the reference image) against the intent, and its
		/// corrections trigger a full re-plan. Catches what code checks cannot:
		/// upside-down details, depth shifts, wrong proportions on a clean pass.
		/// </summary>
		public int MaxReviewRounds { get; init; } = 1;

		/// <summary>Declared-volume cap above which a planned layers part is demoted to a script part.</summary>
		public int PartVoxelBudget { get; init; } = 4000;

		/// <summary>
		/// Read the authoritative brief fields (silhouette occupancy + palette)
		/// deterministically from the reference pixels rather than via a vision
		/// call. Default on: reference images are plain-background flat-colour art,
		/// for which thresholding + quantization is reproducible, free, and pixel-
		/// exact, where the vision read needed retry/symmetrise/trim scaffolding.
		/// </summary>
		public bool DeterministicBrief { get; init; } = true;

		/// <summary>
		/// When <see cref="DeterministicBrief"/> is on, additionally make one slim
		/// vision call for the advisory semantic fields (proportions, signature
		/// features). Off by default — those fields are guidance only and the
		/// pipeline treats their absence gracefully, so the call is pure cost.
		/// </summary>
		public bool ExtractSemanticBriefFields { get; init; }

		/// <summary>
		/// Cell solid when more than this fraction of its pixels are foreground.
		/// Per-cell area coverage (vs single-sample) anti-aliases edges cleanly.
		/// </summary>
		public float SilhouetteCellCoverage { get; init; } = 0.5f;

		/// <summary>
		/// Normalised RGB distance from the corner-sampled background colour beyond
		/// which an opaque-image pixel counts as foreground. Tolerance, not exact
		/// match, so a gradient/noisy "plain" background still keys. Ignored when
		/// the image carries real transparency (the alpha channel keys it exactly).
		/// </summary>
		public float BackgroundColourTolerance { get; init; } = 0.12f;

		public float SilhouetteIouThreshold { get; init; } = 0.75f;

		/// <summary>
		/// Minimum fraction of reference-silhouette cells the planned part boxes
		/// must be able to reach — below this the plan is bounced back to the
		/// planner, since no authoring can fill cells outside every box. Kept
		/// forgiving because the silhouette is a vision guess that tends to blob
		/// gaps solid; a wrong overall width is checked separately and strictly.
		/// </summary>
		public float SilhouetteCoverageThreshold { get; init; } = 0.8f;

		/// <summary>
		/// Operator-supplied set-wide style guidance ("prefer simplicity, rounded
		/// boxes where possible, only as many parts as needed"), injected into the
		/// planning, authoring, and review prompts. Empty = none.
		/// </summary>
		public string StyleGuidance { get; init; } = string.Empty;

		public VoxelScriptLimits ScriptLimits { get; init; } = VoxelScriptLimits.Default;

		public static VoxelizationConfig Default { get; } = new();

		/// <summary>The model id a usage-tracker stage name resolves to, for cost estimation.</summary>
		public string ModelForStage(string stage) => stage switch
		{
			ManifestGenerator.Stage => ManifestModel,
			RunFolderNamer.Stage => ManifestModel,
			BriefExtractor.Stage => PlanningModel,
			ModelPlanner.Stage => PlanningModel,
			ModelRefiner.Stage => PlanningModel,
			SetOrchestrator.ReviewStage => PlanningModel,
			_ => AuthoringModel,
		};
	}

	public sealed class VoxelizationException : System.Exception
	{
		public VoxelizationException(string message) : base(message)
		{
		}

		public VoxelizationException(string message, System.Exception inner) : base(message, inner)
		{
		}
	}
}
