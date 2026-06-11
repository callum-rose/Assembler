using System.Collections.Generic;
using System.Linq;
using Assembler.Anthropic;

namespace Assembler.Voxelization
{
	public sealed record StageUsage(string Stage, int Requests, AnthropicTokenUsage Tokens);

	/// <summary>
	/// Thread-safe per-stage token accounting, fed by the gateway from day one
	/// (Decision 10) and surfaced in the review gallery.
	/// </summary>
	public sealed class TokenUsageTracker
	{
		private readonly object _gate = new();
		private readonly Dictionary<string, StageUsage> _stages = new();

		public void Record(string stage, AnthropicTokenUsage usage)
		{
			lock (_gate)
			{
				_stages[stage] = _stages.TryGetValue(stage, out var existing)
					? existing with { Requests = existing.Requests + 1, Tokens = existing.Tokens.Add(usage) }
					: new StageUsage(stage, 1, usage);
			}
		}

		public IReadOnlyList<StageUsage> Snapshot()
		{
			lock (_gate)
			{
				return _stages.Values.OrderBy(s => s.Stage).ToList();
			}
		}

		public AnthropicTokenUsage Total()
		{
			lock (_gate)
			{
				return _stages.Values.Aggregate(AnthropicTokenUsage.Zero, (sum, s) => sum.Add(s.Tokens));
			}
		}
	}
}
