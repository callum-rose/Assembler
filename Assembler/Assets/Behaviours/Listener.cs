using System;
using System.Collections.Generic;
using Assembler.Resolving;

namespace Assembler.Behaviours
{
	public abstract class Listener
	{
		private IReadOnlyDictionary<string, string> OutputMapping { get; }

		protected Listener(IReadOnlyDictionary<string, string> outputMapping)
		{
			OutputMapping = outputMapping;
		}

		public abstract void Notify(TriggerContext ctx);

		protected TriggerContext Prepare(TriggerContext ctx) =>
			OutputMapping.Count > 0 ? ctx.WithRenamed(OutputMapping) : ctx;
	}

	public sealed class DirectListener : Listener
	{
		private readonly GameBehaviour _target;

		public DirectListener(GameBehaviour target,
			IReadOnlyDictionary<string, string> outputMapping) : base(outputMapping)
		{
			_target = target;
		}

		public override void Notify(TriggerContext ctx)
		{
			using var _ = new TriggerContextScope(Prepare(ctx));
			_target.Execute();
		}
	}

	public sealed class EntityTaggedListener : Listener
	{
		private readonly IValueProvider<string> _entityTag;
		private readonly string? _behaviourId;
		private readonly Func<string, string, IReadOnlyList<GameBehaviour>> _resolveTargets;

		public EntityTaggedListener(IValueProvider<string> entityTag,
			string? behaviourId,
			Func<string, string, IReadOnlyList<GameBehaviour>> resolveTargets,
			IReadOnlyDictionary<string, string> outputMapping) : base(outputMapping)
		{
			_entityTag = entityTag;
			_behaviourId = behaviourId;
			_resolveTargets = resolveTargets;
		}

		public override void Notify(TriggerContext ctx)
		{
			using var _ = new TriggerContextScope(Prepare(ctx));

			var entityTag = _entityTag.Value;

			if (!string.IsNullOrEmpty(_behaviourId))
			{
				InvokeAll(_resolveTargets(entityTag, _behaviourId));
			}
		}

		private static void InvokeAll(IReadOnlyList<GameBehaviour> targets)
		{
			foreach (var behaviour in targets)
			{
				if (behaviour != null)
				{
					behaviour.Execute();
				}
			}
		}
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
			using var _ = new TriggerContextScope(Prepare(ctx));
			InvokeAll(_resolveTargets(_behaviourTag.Value));
		}

		private static void InvokeAll(IReadOnlyList<GameBehaviour> targets)
		{
			foreach (var behaviour in targets)
			{
				if (behaviour != null)
				{
					behaviour.Execute();
				}
			}
		}
	}
}
