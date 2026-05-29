namespace Assembler.Resolving
{
	public sealed class ValueProvider<T> : IValueProvider<T>
	{
		private T _value;

		public ValueProvider(T value)
		{
			_value = value;
		}

		public T Get(TriggerContext ctx) => _value;

		object IValueProvider.Get(TriggerContext ctx) => _value!;

		public void Set(T value) => _value = value;
	}
}
