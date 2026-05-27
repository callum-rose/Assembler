using System.Text.RegularExpressions;

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
		private static readonly Regex YamlBlock = new(
			@"```yaml\s*\r?\n(?<body>.*?)```",
			RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static readonly Regex FeedbackBlock = new(
			@"```feedback\s*\r?\n(?<body>.*?)```",
			RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public static ExtractedResponse Extract(string assistantText)
		{
			var yaml = Match(assistantText, YamlBlock);
			var feedback = Match(assistantText, FeedbackBlock);
			return new ExtractedResponse(yaml, feedback);
		}

		private static string? Match(string text, Regex regex)
		{
			var m = regex.Match(text);
			if (!m.Success)
			{
				return null;
			}
			return m.Groups["body"].Value.TrimEnd('\r', '\n');
		}
	}
}
