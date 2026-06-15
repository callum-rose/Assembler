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

		/// <summary>
		/// How far a manifest-pinned axis may disagree with a reference silhouette's
		/// own aspect (as a fraction of the pinned extent) before the asset is failed
		/// up front as an inconsistent input, rather than squashing the silhouette
		/// into the box and failing the coverage gate later. 0 = exact; a large value
		/// effectively disables the pre-check.
		/// </summary>
		public float ReferenceAspectTolerance { get; init; } = 0.15f;

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
		/// Forward per-part hull bound (B2, default on): when the asset has a brief
		/// with silhouettes, each part is born inside the reference envelope — its
		/// authoring prompt carries the part's slice of the hull as a hard mask, and
		/// a deterministic per-part cap clips any overhang in the assemble path
		/// before composition. Non-destructive and reproducible from the exported
		/// model + brief; off restores free-authoring + the IoU gate alone.
		/// </summary>
		public bool EnableForwardHullBound { get; init; } = true;

		/// <summary>
		/// Pre-authoring feasibility floor: a non-loose planned part whose box has
		/// less than this fraction inside the reference silhouette is bounced back to
		/// the planner (its box sits largely outside the envelope) before any
		/// authoring spend. Loose parts (foliage/scatter) are exempt.
		/// </summary>
		public float HullPartSolidFloor { get; init; } = 0.10f;

		/// <summary>
		/// Master toggle for the B1 post-compose hull clip, retained as a config-gated
		/// backstop. Default OFF now that the forward bound (B2) authors in-envelope;
		/// flip on for a per-run A/B against the old whole-model post-compose trim.
		/// </summary>
		public bool EnableHullClip { get; init; }

		/// <summary>Silhouette-mask dilation in cells; matches the ±1 planner width slack so an on-edge voxel survives.</summary>
		public int HullClipDilation { get; init; } = 1;

		/// <summary>Per-part removed-fraction at/above which a clip is a moderate trim (suggests a reposition).</summary>
		public float HullClipModerateRatio { get; init; } = 0.20f;

		/// <summary>Per-part removed-fraction at/above which a clip is refused (severe — keep authored, re-plan).</summary>
		public float HullClipSevereRatio { get; init; } = 0.50f;

		/// <summary>Discard the whole hull when the aggregate removed-mass fraction exceeds this (bad reference).</summary>
		public float HullClipGlobalFloor { get; init; } = 0.30f;

		/// <summary>The hull-clip thresholds projected onto the pure clip's settings.</summary>
		public HullClipSettings HullClipSettings =>
			new(HullClipDilation, HullClipModerateRatio, HullClipSevereRatio, HullClipGlobalFloor);

		/// <summary>
		/// Operator-supplied set-wide style guidance ("prefer simplicity, rounded
		/// boxes where possible, only as many parts as needed"), injected into the
		/// manifest, planning, authoring, and review prompts. It is also the
		/// authority on scale at the manifest stage. Empty = none.
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
