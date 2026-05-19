namespace Assembler.Resolving
{
	public sealed class ValueProvider<T> : IValueProvider<T>
	{
		public T Value { get; set; }

		object IValueProvider.Value => Value!;

		public ValueProvider(T value)
		{
			Value = value;
		}
	}
}
