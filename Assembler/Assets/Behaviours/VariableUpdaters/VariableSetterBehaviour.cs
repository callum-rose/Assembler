using Assembler.Core;
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
	public abstract class VariableSetterBehaviour<TValue> : GameBehaviour<VariableSetterData<TValue>>, IAmExecutable
	{
		public void Execute(TriggerContext ctx)
		{
			var value = Data.ValueToGet.Get(ctx);
			Data.ValueToSet.Set(value);
		}
	}

	/// <summary>Writes a bool value into the referenced variable when Executed. See <see cref="VariableSetterBehaviour{TValue}"/>.</summary>
	public class BoolSetter : VariableSetterBehaviour<bool> { }

	/// <summary>Writes a Color value into the referenced variable when Executed. See <see cref="VariableSetterBehaviour{TValue}"/>.</summary>
	public class ColourSetter : VariableSetterBehaviour<Color> { }

	/// <summary>Writes a float value into the referenced variable when Executed. See <see cref="VariableSetterBehaviour{TValue}"/>.</summary>
	public class FloatSetter : VariableSetterBehaviour<float> { }

	/// <summary>Writes an int value into the referenced variable when Executed. See <see cref="VariableSetterBehaviour{TValue}"/>.</summary>
	public class IntSetter : VariableSetterBehaviour<int> { }

	/// <summary>Writes a record value into the referenced variable when Executed. See <see cref="VariableSetterBehaviour{TValue}"/>.</summary>
	public class RecordSetter : VariableSetterBehaviour<Record> { }

	/// <summary>Writes a string value into the referenced variable when Executed. See <see cref="VariableSetterBehaviour{TValue}"/>.</summary>
	public class StringSetter : VariableSetterBehaviour<string> { }

	/// <summary>Writes a Vector3 value into the referenced variable when Executed. See <see cref="VariableSetterBehaviour{TValue}"/>.</summary>
	public class Vector3Setter : VariableSetterBehaviour<Vector3> { }
}
