using System;
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

	public readonly struct TriggerContextScope : IDisposable
	{
		private readonly TriggerContext? _previous;

		public TriggerContextScope(TriggerContext ctx)
		{
			_previous = Current;
			Current = ctx;
		}

		public static TriggerContext? Current { get; private set; }

		public void Dispose() => Current = _previous;
	}
}
