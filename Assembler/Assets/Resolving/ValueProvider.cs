namespace Assembler.Resolving
{
	public sealed class ValueProvider<T> : IValueProvider<T>
	{
		public T Value { get; set; }

		public ValueProvider(T value)
		{
			Value = value;
		}

		public void Set(T value)
		{
			Value = value;
		}
	}
}