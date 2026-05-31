using System;
using System.Collections.Generic;
using Assembler.Resolving;

namespace Assembler.Behaviours
{
	public abstract class Listener
	{
		private readonly IReadOnlyDictionary<string, string> _outputMapping;

		/// <summary>Branch channel this listener belongs to; consulted by routing behaviours such as <c>branch</c>.</summary>
		public bool When { get; init; } = true;

		protected Listener(IReadOnlyDictionary<string, string> outputMapping)
		{
			_outputMapping = outputMapping;
		}

		public abstract void Notify(TriggerContext ctx);

		protected TriggerContext Prepare(TriggerContext ctx) => ctx.WithRenamed(_outputMapping);
	}

	public sealed class DirectListener : Listener
	{
		private readonly GameBehaviour _target;

		public DirectListener(GameBehaviour target, IReadOnlyDictionary<string, string> outputMapping) : base(outputMapping)
		{
			_target = target;
		}

		public override void Notify(TriggerContext ctx) => _target.Execute(Prepare(ctx));
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
			if (_behaviourId == null)
			{
				return;
			}

			var preparedCtx = Prepare(ctx);
			var targets = _resolveTargets(_entityTag.Get(preparedCtx), _behaviourId);

			foreach (var behaviour in targets)
			{
				if (behaviour != null)
				{
					behaviour.Execute(preparedCtx);
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
					behaviour.Execute(preparedCtx);
				}
			}
		}
	}
}
