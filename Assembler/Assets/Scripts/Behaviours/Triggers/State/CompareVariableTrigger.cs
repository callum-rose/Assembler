using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using AssemblerAlpha.Variables;

namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public abstract class CompareVariableTrigger<TValue> : Trigger<ConditionTriggerInfo>
	{
		protected GameVariable<TValue> Variable { get; private set; }
		protected GameVariable<TValue> OtherVariable { get; private set; }
		
		protected override void OnInitialise(ConditionTriggerInfo behaviourInfo)
		{
			Variable.Changed += OnEitherVariableChanged;
			OtherVariable.Changed += OnEitherVariableChanged;
		}
		
		protected abstract void OnEitherVariableChanged();
	}
}