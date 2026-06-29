namespace Assembler.VoxelPipeline
{
    /// <summary>
    /// A single post-processing step: a pure-ish transform over the dense <see cref="VoxModel"/>.
    /// Steps run in a fixed canonical order via <see cref="VoxPipeline"/>; each instance carries
    /// its own config (enabled flag + params), so the pipeline is simply an ordered list of these.
    /// </summary>
    public interface IVoxStep
    {
        /// <summary>Human-readable name, used for progress reporting.</summary>
        string Name { get; }

        /// <summary>Whether the pipeline should run this step.</summary>
        bool Enabled { get; }

        /// <summary>Mutates <paramref name="model"/> in place.</summary>
        void Apply(VoxModel model);
    }
}
