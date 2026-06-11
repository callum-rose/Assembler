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
		public int MaxPartAttempts { get; init; } = 2;

		/// <summary>Validation-driven re-author rounds per model after the first assembly.</summary>
		public int MaxValidationRounds { get; init; } = 2;

		/// <summary>Declared-volume cap above which a planned layers part is demoted to a script part.</summary>
		public int PartVoxelBudget { get; init; } = 4000;

		public float SilhouetteIouThreshold { get; init; } = 0.75f;

		public VoxelScriptLimits ScriptLimits { get; init; } = VoxelScriptLimits.Default;

		public static VoxelizationConfig Default { get; } = new();
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
