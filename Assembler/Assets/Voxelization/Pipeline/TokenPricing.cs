using Assembler.Anthropic;

namespace Assembler.Voxelization
{
	/// <summary>USD per million tokens. Cache reads are ~0.1x the input rate; 5-minute-TTL cache writes are 1.25x.</summary>
	public sealed record TokenRates(double InputPerMTok, double OutputPerMTok)
	{
		public double CacheReadPerMTok => InputPerMTok * 0.1;
		public double CacheWritePerMTok => InputPerMTok * 1.25;
	}

	/// <summary>
	/// Cost estimation for tracked token usage. The Anthropic API exposes no
	/// account balance/credit endpoint to regular API keys, so the review
	/// window shows estimated spend instead of a remaining balance. Rates
	/// cached 2026-06 from platform.claude.com/docs/en/pricing.
	/// </summary>
	public static class TokenPricing
	{
		public static TokenRates RatesFor(string model) => model switch
		{
			var m when m.Contains("haiku") => new TokenRates(1.0, 5.0),
			var m when m.Contains("opus") => new TokenRates(5.0, 25.0),
			var m when m.Contains("fable") => new TokenRates(10.0, 50.0),
			_ => new TokenRates(3.0, 15.0), // sonnet
		};

		public static double EstimateUsd(AnthropicTokenUsage usage, TokenRates rates) =>
			(usage.InputTokens * rates.InputPerMTok
			 + usage.OutputTokens * rates.OutputPerMTok
			 + usage.CacheReadInputTokens * rates.CacheReadPerMTok
			 + usage.CacheCreationInputTokens * rates.CacheWritePerMTok) / 1_000_000.0;
	}
}
