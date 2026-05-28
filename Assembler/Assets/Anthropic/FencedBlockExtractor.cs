using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Assembler.Anthropic
{
	public static class FencedBlockExtractor
	{
		private readonly static ConcurrentDictionary<string, Regex> Cache = new();

		public static string? Extract(string text, string blockName)
		{
			var regex = Cache.GetOrAdd(blockName, name => new Regex(
				$@"```{Regex.Escape(name)}\s*\r?\n(?<body>.*?)```",
				RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase));

			return regex.Match(text) is { Success: true } m
				? m.Groups["body"].Value.TrimEnd('\r', '\n')
				: null;
		}
	}
}
