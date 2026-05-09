namespace Assembler.Parsing.Phase3;

public sealed class ValueContainer<T>
{
	public event Action<T>? ValueChanged;

	public T Value
	{
		get => field;
		set
		{
			if (EqualityComparer<T>.Default.Equals(field, value))
			{
				return;
			}

			field = value;
			ValueChanged?.Invoke(field);
		}
	}
	
	public ValueContainer(T initialValue)
	{
		Value = initialValue;
	}
}