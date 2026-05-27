using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.VariableUpdaters
{
	/// <summary>Adds <c>Delta</c> to the variable referenced by <c>VariableId</c> when Executed.</summary>
	/// <remarks>
	/// Properties:
	///   VariableId: Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game.
	///   Delta: Amount to add to the current value of the variable. Can be a constant, expression, or another variable reference. Negative values subtract.
	/// </remarks>
	public abstract class VariableAdjustBehaviour<TValue> : GameBehaviour<VariableAdjustData<TValue>>
	{
		public override void Execute()
		{
			Data.ValueToSet.Value = Add(Data.ValueToSet.Value, Data.Delta.Value);
			UnityEngine.Debug.Log($"{Id} adjusted to {Data.ValueToSet.Value}");
		}

		protected abstract TValue Add(TValue current, TValue delta);
	}
}
