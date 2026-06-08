using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.VariableUpdaters
{
	/// <summary>Writes <c>Value</c> into the variable referenced by <c>VariableId</c> when Executed.</summary>
	/// <remarks>
	/// Properties:
	///   VariableId: Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game.
	///   Value: Source value to assign. Can be a constant, expression, or another variable reference.
	/// </remarks>
	public abstract class VariableSetterBehaviour<TValue> : GameBehaviour<VariableSetterData<TValue>>
	{
		public override void Execute(TriggerContext ctx)
		{
			var value = Data.ValueToGet.Get(ctx);
			Data.ValueToSet.Set(value);
			UnityEngine.Debug.Log($"{Id} set to {value}");
		}
	}
}
