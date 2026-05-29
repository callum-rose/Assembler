using System.Collections.Generic;
using System.Collections.Immutable;

namespace Assembler.Resolving
{
	public sealed class TriggerContext
	{
		public static readonly TriggerContext Empty = new(ImmutableDictionary<string, object>.Empty);

		private readonly ImmutableDictionary<string, object> _values;

		private TriggerContext(ImmutableDictionary<string, object> values) => _values = values;

		public TriggerContext With(string key, object value) =>
			new(_values.SetItem(key, value));

		public TriggerContext WithMany(IEnumerable<KeyValuePair<string, object>> kvps) =>
			new(_values.SetItems(kvps));

		public TriggerContext WithRenamed(IReadOnlyDictionary<string, string> rename)
		{
			if (rename == null || rename.Count == 0)
			{
				return this;
			}

			var builder = _values.ToBuilder();

			foreach (var (from, to) in rename)
			{
				if (_values.TryGetValue(from, out var value))
				{
					builder[to] = value;
				}
			}

			return new TriggerContext(builder.ToImmutable());
		}

		public TriggerContext Without(string key) =>
			new(_values.Remove(key));

		public bool TryGet<T>(string key, out T value)
		{
			if (_values.TryGetValue(key, out var raw))
			{
				value = (T)raw;
				return true;
			}

			value = default!;
			return false;
		}

		public T Get<T>(string key)
		{
			if (!_values.TryGetValue(key, out var raw))
			{
				throw new KeyNotFoundException($"Trigger output '{key}' not found in current context");
			}

			return (T)raw;
		}
	}

	/// <summary>
	/// Per-behaviour mutable slot that holds the trigger context currently being processed by that behaviour.
	/// A fresh holder is allocated for every <see cref="Assembler.Behaviours.GameBehaviour"/> at build time and
	/// shared with every <see cref="TriggerOutputProvider{T}"/> that the behaviour's data depends on, so that
	/// <c>!output</c> reads route to the same context that the invoking listener handed in.
	/// </summary>
	public sealed class TriggerContextHolder
	{
		public TriggerContext Current { get; set; } = TriggerContext.Empty;
	}
}
