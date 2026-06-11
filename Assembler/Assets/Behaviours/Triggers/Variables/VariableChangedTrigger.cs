using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Variables
{
	/// <summary>Fires whenever the referenced variable's value changes, pushing the new value to listeners.</summary>
	/// <remarks>
	/// Properties:
	///   VariableId: The variable to watch. Must be a writable `!var` reference of the matching type.
	/// Outputs:
	///   value [T]: The variable's new value.
	///   previous [T]: The variable's value immediately before the change.
	/// </remarks>
	public abstract class VariableChangedTrigger<T> : Trigger<VariableChangedTriggerData<T>>
	{
		// The builder guarantees the resolved variable is observable (it hard-fails otherwise), so this cast is safe.
		private IObservableValueProvider<T>? _observable;

		protected override void OnInitialise(VariableChangedTriggerData<T> data)
		{
			_observable = (IObservableValueProvider<T>)data.Variable;
			_observable.Changed += OnVariableChanged;
		}

		private void OnVariableChanged(T previous, T current) =>
			NotifyListeners(TriggerContext.New(b =>
			{
				b["value"] = current!;
				b["previous"] = previous!;
			}));

		private void OnDestroy()
		{
			if (_observable != null)
			{
				_observable.Changed -= OnVariableChanged;
			}
		}
	}

	/// <summary>Fires when an int variable changes. See <see cref="VariableChangedTrigger{T}"/>.</summary>
	public class IntVariableChangedTrigger : VariableChangedTrigger<int> { }

	/// <summary>Fires when a float variable changes. See <see cref="VariableChangedTrigger{T}"/>.</summary>
	public class FloatVariableChangedTrigger : VariableChangedTrigger<float> { }

	/// <summary>Fires when a bool variable changes. See <see cref="VariableChangedTrigger{T}"/>.</summary>
	public class BoolVariableChangedTrigger : VariableChangedTrigger<bool> { }

	/// <summary>Fires when a string variable changes. See <see cref="VariableChangedTrigger{T}"/>.</summary>
	public class StringVariableChangedTrigger : VariableChangedTrigger<string> { }

	/// <summary>Fires when a vector variable changes. See <see cref="VariableChangedTrigger{T}"/>.</summary>
	public class Vector3VariableChangedTrigger : VariableChangedTrigger<Vector3> { }

	/// <summary>Fires when a colour variable changes. See <see cref="VariableChangedTrigger{T}"/>.</summary>
	public class ColourVariableChangedTrigger : VariableChangedTrigger<Color> { }
}
