using System;
using System.Collections.Generic;

namespace Assembler.Resolving
{
	public class TriggerContext
	{
		private readonly Stack<Dictionary<string, object>> _stack = new();

		public Scope Push()
		{
			_stack.Push(new Dictionary<string, object>());
			return new Scope(this);
		}

		private void Pop()
		{
			if (_stack.Count > 0)
			{
				_stack.Pop();
			}
		}

		public readonly struct Scope : IDisposable
		{
			private readonly TriggerContext _context;

			public Scope(TriggerContext context) => _context = context;

			public void Dispose() => _context.Pop();
		}

		public void Set(string key, object value) => _stack.Peek()[key] = value;

		public T Get<T>(string key)
		{
			if (_stack.Count == 0)
			{
				throw new InvalidOperationException("No trigger context available — !output can only be used in behaviours invoked by a data-producing trigger");
			}

			if (!_stack.Peek().TryGetValue(key, out var val))
			{
				throw new KeyNotFoundException($"Trigger output '{key}' not found in current context");
			}

			return (T)val;
		}

		public void ApplyMapping(IReadOnlyDictionary<string, string> mapping)
		{
			var frame = _stack.Peek();

			foreach (var (nativeName, mappedName) in mapping)
			{
				if (frame.TryGetValue(nativeName, out var value))
				{
					frame[mappedName] = value;
				}
			}
		}
	}
}
