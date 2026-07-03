using Assembler.Anthropic;

namespace Assembler.AssetGeneration.MeshToVoxel.Generation
{
    /// <summary>
    /// Pulls the single fenced <c>```json</c> block out of an assistant reply, or <c>null</c>
    /// when absent. Thin wrapper over <see cref="FencedBlockExtractor"/> so the json-block
    /// convention lives in one place.
    /// </summary>
    public static class ConfigExtractor
    {
        public static string? Extract(string text) => FencedBlockExtractor.Extract(text, "json");
    }
}
