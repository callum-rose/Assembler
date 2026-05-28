using System;
using System.Collections.Generic;
using Assembler.Resolving;

namespace Assembler.Behaviours
{
	public abstract class Listener
	{
		public IReadOnlyDictionary<string, string> OutputMapping { get; }
		protected TriggerContext TriggerContext { get; }

		protected Listener(IReadOnlyDictionary<string, string> outputMapping, TriggerContext triggerContext)
		{
			OutputMapping = outputMapping;
			TriggerContext = triggerContext;
		}

		public abstract void Notify();

		protected void ApplyOutputMapping()
		{
			if (OutputMapping.Count > 0)
			{
				TriggerContext.ApplyMapping(OutputMapping);
			}
		}
	}

	public sealed class DirectListener : Listener
	{
		public GameBehaviour Target { get; }

		public DirectListener(GameBehaviour target,
			IReadOnlyDictionary<string, string> outputMapping,
			TriggerContext triggerContext) : base(outputMapping, triggerContext)
		{
			Target = target;
		}

		public override void Notify()
		{
			ApplyOutputMapping();
			Target.Execute();
		}
	}

	public sealed class EntityTaggedListener : Listener
	{
		public IValueProvider<string> EntityTag { get; }
		public string? BehaviourId { get; }
		private readonly Func<string, string, IReadOnlyList<GameBehaviour>> _resolveTargets;

		public EntityTaggedListener(IValueProvider<string> entityTag,
			string? behaviourId,
			Func<string, string, IReadOnlyList<GameBehaviour>> resolveTargets,
			IReadOnlyDictionary<string, string> outputMapping,
			TriggerContext triggerContext) : base(outputMapping, triggerContext)
		{
			EntityTag = entityTag;
			BehaviourId = behaviourId;
			_resolveTargets = resolveTargets;
		}

		public override void Notify()
		{
			ApplyOutputMapping();

			var entityTag = EntityTag.Value;
			if (entityTag == null || string.IsNullOrEmpty(BehaviourId))
			{
				return;
			}

			InvokeAll(_resolveTargets(entityTag, BehaviourId));
		}

		private static void InvokeAll(IReadOnlyList<GameBehaviour> targets)
		{
			foreach (var behaviour in targets)
			{
				if (behaviour)
				{
					behaviour.Execute();
				}
			}
		}
	}

	public sealed class BehaviourTaggedListener : Listener
	{
		public IValueProvider<string> BehaviourTag { get; }
		private readonly Func<string, IReadOnlyList<GameBehaviour>> _resolveTargets;

		public BehaviourTaggedListener(IValueProvider<string> behaviourTag,
			Func<string, IReadOnlyList<GameBehaviour>> resolveTargets,
			IReadOnlyDictionary<string, string> outputMapping,
			TriggerContext triggerContext) : base(outputMapping, triggerContext)
		{
			BehaviourTag = behaviourTag;
			_resolveTargets = resolveTargets;
		}

		public override void Notify()
		{
			ApplyOutputMapping();

			var behaviourTag = BehaviourTag.Value;
			if (behaviourTag == null)
			{
				return;
			}

			InvokeAll(_resolveTargets(behaviourTag));
		}

		private static void InvokeAll(IReadOnlyList<GameBehaviour> targets)
		{
			foreach (var behaviour in targets)
			{
				if (behaviour)
				{
					behaviour.Execute();
				}
			}
		}
	}
}
