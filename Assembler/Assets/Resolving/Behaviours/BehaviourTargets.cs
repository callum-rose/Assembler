using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	/// <summary>
	/// The live set of behaviour components a behaviour acts on — e.g. to flip their
	/// <see cref="Behaviour.enabled"/> state. Wraps a build-time closure over the behaviour registry
	/// (mirroring <see cref="CameraTargetProvider"/>), so tag-based targets re-query live state on each
	/// call and pick up entities spawned after build. Unlike listener targets these need not be
	/// executable, so self-driven behaviours can be targeted.
	/// </summary>
	public sealed class BehaviourTargets
	{
		private readonly Func<TriggerContext, IReadOnlyList<Behaviour>> _resolve;

		public BehaviourTargets(Func<TriggerContext, IReadOnlyList<Behaviour>> resolve) => _resolve = resolve;

		public IReadOnlyList<Behaviour> Resolve(TriggerContext ctx) => _resolve(ctx);
	}
}
