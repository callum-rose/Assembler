using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Assembler.Resolving
{
	public sealed class TriggerContext
	{
		public readonly static TriggerContext Empty = new(ImmutableDictionary<string, object>.Empty);

		private readonly ImmutableDictionary<string, object> _values;

		private TriggerContext(ImmutableDictionary<string, object> values) => _values = values;

		/// <summary>The keys currently present in this context. Used by debug tooling to show what a fired trigger carried.</summary>
		public IEnumerable<string> Keys => _values.Keys;
		
		public static TriggerContext New(string key, object value) => Empty.With(key, value);

		public static TriggerContext New(Action<ImmutableDictionary<string, object>.Builder> builder) => Empty.With(builder);

		public TriggerContext With(string key, object value) => new(_values.SetItem(key, value));

		public TriggerContext WithMany(IEnumerable<KeyValuePair<string, object>> kvps) => new(_values.SetItems(kvps));

		/// <summary>
		/// Batch-update the context in a single immutable allocation. Use this when a trigger emits multiple
		/// outputs per fire (e.g. a collision setting four keys at once) to avoid the intermediate dictionaries
		/// that chained <c>.With(...)</c> calls would produce.
		/// </summary>
		public TriggerContext With(Action<ImmutableDictionary<string, object>.Builder> build)
		{
			var builder = _values.ToBuilder();
			build(builder);
			return new TriggerContext(builder.ToImmutable());
		}

		public TriggerContext WithRenamed(IReadOnlyDictionary<string, string> rename)
		{
			if (rename.Count == 0)
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
}