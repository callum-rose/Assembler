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

		public float SilhouetteIouThreshold { get; init; } = 0.75f;

		/// <summary>
		/// Minimum fraction of reference-silhouette cells the planned part boxes
		/// must be able to reach — below this the plan is bounced back to the
		/// planner, since no authoring can fill cells outside every box. Kept
		/// forgiving because the silhouette is a vision guess that tends to blob
		/// gaps solid; a wrong overall width is checked separately and strictly.
		/// </summary>
		public float SilhouetteCoverageThreshold { get; init; } = 0.8f;

		public VoxelScriptLimits ScriptLimits { get; init; } = VoxelScriptLimits.Default;

		public static VoxelizationConfig Default { get; } = new();

		/// <summary>The model id a usage-tracker stage name resolves to, for cost estimation.</summary>
		public string ModelForStage(string stage) => stage switch
		{
			ManifestGenerator.Stage => ManifestModel,
			BriefExtractor.Stage => PlanningModel,
			ModelPlanner.Stage => PlanningModel,
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
