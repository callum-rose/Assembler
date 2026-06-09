namespace Assembler.Resolving.Behaviours
{
	public sealed class VariableChangedTriggerData<T> : TriggerData
	{
		public IValueProvider<T> Variable { get; }

		public VariableChangedTriggerData(string id, IValueProvider<T> variable) :
			base(id) => Variable = variable;
	}
}
