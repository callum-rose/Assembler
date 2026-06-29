using Assembler.Anthropic;

namespace Assembler.VoxelPipeline.Generation
{
    /// <summary>
    /// Pulls the single fenced <c>```json</c> block out of an assistant reply, or <c>null</c>
    /// when absent. Thin wrapper over <see cref="FencedBlockExtractor"/> so the json-block
    /// convention lives in one place.
    /// </summary>
    public static class VoxConfigExtractor
    {
        public static string? Extract(string text) => FencedBlockExtractor.Extract(text, "json");
    }
}
