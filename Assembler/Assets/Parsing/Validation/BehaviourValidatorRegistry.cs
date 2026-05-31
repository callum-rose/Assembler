using System;
using System.Collections.Generic;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Parsing.Validation.Behaviours;

namespace Assembler.Parsing.Validation
{
	/// <summary>
	/// Opt-in catalogue of behaviour-specific validators, keyed by the concrete
	/// <see cref="Info.BehaviourInfo"/> type. Behaviours absent from this map still receive the shared
	/// base checks run by <see cref="GameInfoValidator"/>; add an entry here only when a behaviour has
	/// required values or invariants of its own.
	/// </summary>
	public static class BehaviourValidatorRegistry
	{
		public readonly static IReadOnlyDictionary<Type, IBehaviourValidator> All =
			new Dictionary<Type, IBehaviourValidator>
			{
				[typeof(AddForceInfo)] = new AddForceValidator(),
				[typeof(SpawnerInfo)] = new SpawnerValidator(),
				[typeof(KeyHoldTriggerInfo)] = new KeyHoldTriggerValidator(),
			};
	}
}
