using System;
using System.Collections.Generic;
using Assembler.Resolving;

namespace Assembler.Behaviours
{
	public abstract class Listener
	{
		private readonly IReadOnlyDictionary<string, string> _outputMapping;

		protected Listener(IReadOnlyDictionary<string, string> outputMapping)
		{
			_outputMapping = outputMapping;
		}

		public abstract void Notify(TriggerContext ctx);

		/// <summary>
		/// The behaviours this listener currently resolves to, without notifying them. A direct listener
		/// yields its single target; tagged listeners re-query the live registry against <paramref name="ctx"/>,
		/// so the set reflects entities present at call time (empty when the tag is absent/unmatched). Used by
		/// behaviours that act on their targets directly — e.g. enabling/disabling them — rather than executing
		/// them, so resolved targets need not be <see cref="IAmExecutable"/>. The base resolves to nothing; the
		/// concrete listener kinds override.
		/// </summary>
		public virtual IReadOnlyList<GameBehaviour> ResolveTargets(TriggerContext ctx) => Array.Empty<GameBehaviour>();

		protected TriggerContext Prepare(TriggerContext ctx) => ctx.WithRenamed(_outputMapping);

#if DEBUG_CONSOLE
		/// <summary>
		/// The behaviours this listener currently resolves to. Debug-only graph inspection; resolves against
		/// an empty context, so tags that depend on trigger output may not resolve.
		/// </summary>
		public abstract IEnumerable<GameBehaviour> DebugTargets();
#endif
	}

	public sealed class DirectListener : Listener
	{
		private readonly GameBehaviour _target;

		public DirectListener(GameBehaviour target, IReadOnlyDictionary<string, string> outputMapping) : base(outputMapping)
		{
			_target = target;
		}

		// The build wires direct Listeners: targets through the executable guard (see ResolveExecutable), so a
		// non-executable target fails loudly at build; the guard here is a defensive backstop on that contract.
		public override void Notify(TriggerContext ctx) =>
			_target.EnsureExecutable($"targeting behaviour on '{_target.name}'").Execute(Prepare(ctx));

		public override IReadOnlyList<GameBehaviour> ResolveTargets(TriggerContext ctx) =>
			_target != null ? new[] { _target } : Array.Empty<GameBehaviour>();

#if DEBUG_CONSOLE
		public override IEnumerable<GameBehaviour> DebugTargets() => ResolveTargets(TriggerContext.Empty);
#endif
	}

	public sealed class EntityTaggedListener : Listener
	{
		private readonly IValueProvider<string> _entityTag;
		private readonly Func<string, IReadOnlyList<GameBehaviour>> _resolveTargets;

		// _resolveTargets already bakes in the behaviour-id filter: when a BehaviourId was authored it
		// closes over GetByEntityTagAndBehaviourId, and when omitted it is GetByEntityTag (fan out to all
		// behaviours on entities carrying the tag). The decision is made once at wiring time.
		public EntityTaggedListener(IValueProvider<string> entityTag,
			Func<string, IReadOnlyList<GameBehaviour>> resolveTargets,
			IReadOnlyDictionary<string, string> outputMapping) : base(outputMapping)
		{
			_entityTag = entityTag;
			_resolveTargets = resolveTargets;
		}

		public override void Notify(TriggerContext ctx)
		{
			var preparedCtx = Prepare(ctx);
			var entityTag = _entityTag.Get(preparedCtx);
			var targets = _resolveTargets(entityTag);

			foreach (var behaviour in targets)
			{
				if (behaviour != null)
				{
					behaviour.EnsureExecutable(
						$"targeting behaviours on entities tagged '{entityTag}'").Execute(preparedCtx);
				}
			}
		}

		public override IReadOnlyList<GameBehaviour> ResolveTargets(TriggerContext ctx) =>
			_resolveTargets(_entityTag.Get(Prepare(ctx)));

#if DEBUG_CONSOLE
		public override IEnumerable<GameBehaviour> DebugTargets() => ResolveTargets(TriggerContext.Empty);
#endif
	}

	public sealed class BehaviourTaggedListener : Listener
	{
		private readonly IValueProvider<string> _behaviourTag;
		private readonly Func<string, IReadOnlyList<GameBehaviour>> _resolveTargets;

		public BehaviourTaggedListener(IValueProvider<string> behaviourTag,
			Func<string, IReadOnlyList<GameBehaviour>> resolveTargets,
			IReadOnlyDictionary<string, string> outputMapping) : base(outputMapping)
		{
			_behaviourTag = behaviourTag;
			_resolveTargets = resolveTargets;
		}

		public override void Notify(TriggerContext ctx)
		{
			var preparedCtx = Prepare(ctx);
			var tag = _behaviourTag.Get(preparedCtx);

			if (string.IsNullOrEmpty(tag))
			{
				return;
			}

			var targets = _resolveTargets(tag);

			foreach (var behaviour in targets)
			{
				if (behaviour != null)
				{
					behaviour.EnsureExecutable($"targeting behaviours tagged '{tag}'").Execute(preparedCtx);
				}
			}
		}

		public override IReadOnlyList<GameBehaviour> ResolveTargets(TriggerContext ctx)
		{
			var tag = _behaviourTag.Get(Prepare(ctx));
			return string.IsNullOrEmpty(tag) ? Array.Empty<GameBehaviour>() : _resolveTargets(tag);
		}

#if DEBUG_CONSOLE
		public override IEnumerable<GameBehaviour> DebugTargets() => ResolveTargets(TriggerContext.Empty);
#endif
	}
}
