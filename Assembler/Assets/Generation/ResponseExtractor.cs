using Assembler.Anthropic;

namespace Assembler.Generation
{
	public readonly struct ExtractedResponse
	{
		public string? Yaml { get; }
		public string? Feedback { get; }

		public ExtractedResponse(string? yaml, string? feedback)
		{
			Yaml = yaml;
			Feedback = feedback;
		}
	}

	public static class ResponseExtractor
	{
		public static ExtractedResponse Extract(string assistantText) =>
			new(FencedBlockExtractor.Extract(assistantText, "yaml"),
				FencedBlockExtractor.Extract(assistantText, "feedback"));
	}
}
