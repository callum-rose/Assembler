using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using AssemblerAlpha.Variables;

namespace AssemblerAlpha.Behaviours.Triggers.State
{
	public abstract class VariableTrigger<TValue> : Trigger<ConditionTriggerInfo>
	{
		protected GameVariable<TValue> Variable { get; private set; }

		protected override void OnInitialise(ConditionTriggerInfo behaviourInfo)
		{
			Variable.Changed += VariableOnChanged;
		}

		protected abstract void VariableOnChanged();
	}

}