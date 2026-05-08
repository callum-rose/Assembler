using Variables;

namespace Behaviours.Triggers.Variable
{
	public abstract class VariableTrigger<T> : Trigger
	{
		protected GameVariable<T> Variable { get; private set; }

		protected override void OnInitialise(Configuration configuration)
		{
			Variable.Changed += VariableOnChanged;
		}

		protected abstract void VariableOnChanged();
	}

	public abstract class CompareVariableTrigger<T> : Trigger
	{
		protected GameVariable<T> Variable { get; private set; }
		protected GameVariable<T> OtherVariable { get; private set; }
		
		protected override void OnInitialise(Configuration configuration)
		{
			Variable.Changed += OnEitherVariableChanged;
			OtherVariable.Changed += OnEitherVariableChanged;
		}
		
		protected abstract void OnEitherVariableChanged();
	}
}