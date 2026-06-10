using System;

namespace Assembler.Resolving
{
	// Sentinel for an absent value. It is writable (implements IWriteValueProvider) so it can stand in for an
	// unwired writable slot — e.g. an omitted optional !var. Reading throws (there is no value), but writing
	// is a deliberate no-op: a behaviour can Set() unconditionally and the write simply goes nowhere when the
	// slot was never wired, which is cheaper and clearer than every caller null-checking first.
	public class NullValueProvider<T> : IWriteValueProvider<T>
	{
		public static NullValueProvider<T> Instance { get; } = new();

		public T Get(TriggerContext ctx) =>
			throw new InvalidOperationException("Null value provider cannot provide a value");

		object IValueProvider.Get(TriggerContext ctx) =>
			throw new InvalidOperationException("Null value provider cannot provide a value");

		public void Set(T value) { }

		private NullValueProvider() { }
	}
}
