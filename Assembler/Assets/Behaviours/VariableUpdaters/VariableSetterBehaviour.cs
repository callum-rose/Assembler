using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.VariableUpdaters
{
	/// <summary>
	/// Writes <c>Value</c> into the variable referenced by <c>VariableId</c> when Executed.
	/// For conditional assignment ("set X to A if cond else B"), use a single setter whose
	/// <c>Value</c> is an <c>!expr</c> with a ternary body (<c>cond ? A : B;</c>); the expression
	/// compiler supports ternaries (including nested) on every supported variable type, so there
	/// is no need to gate two setters behind a <c>condition gate</c>.
	/// </summary>
	/// <remarks>
	/// Properties:
	///   VariableId: Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game.
	///   Value: Source value to assign. Can be a constant, an `!expr` (including a ternary `cond ? A : B;` for conditional assignment), or another variable reference.
	/// </remarks>
	public abstract class VariableSetterBehaviour<TValue> : GameBehaviour<VariableSetterData<TValue>>
	{
		public override void Execute()
		{
			Data.ValueToSet.Value = Data.ValueToGet.Value;
			UnityEngine.Debug.Log($"{Id} set to {Data.ValueToSet.Value}");
		}
	}
}