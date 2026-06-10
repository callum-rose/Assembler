using System;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Resolving
{
	// Live wrapper backing the !query tag: every read runs the spatial query against the entity index, so a
	// transition condition or steering target reflects the current world. Read-only — Set throws.
	public sealed class QueryValueProvider<T> : IValueProvider<T>
	{
		private readonly EntityQueryService _query;
		private readonly QueryKind _kind;
		private readonly string _entityTag;
		private readonly IValueProvider<Vector3> _origin;
		private readonly IValueProvider<float> _maxRange;

		public QueryValueProvider(
			EntityQueryService query,
			QueryKind kind,
			string entityTag,
			IValueProvider<Vector3> origin,
			IValueProvider<float> maxRange)
		{
			_query = query;
			_kind = kind;
			_entityTag = entityTag;
			_origin = origin;
			_maxRange = maxRange;
		}

		public T Get(TriggerContext ctx) => (T)Read(ctx);

		object IValueProvider.Get(TriggerContext ctx) => Read(ctx);

		public void Set(T value) =>
			throw new InvalidOperationException("!query results are read-only and cannot be set.");

		private object Read(TriggerContext ctx)
		{
			var origin = _origin.Get(ctx);
			var found = _query.TryNearest(origin, _entityTag, _maxRange.Get(ctx), out var id);

			return _kind switch
			{
				QueryKind.NearestId => found ? id : string.Empty,
				// No target: fall back to the origin so a Direction(self, target) reads as zero motion.
				QueryKind.NearestPosition => found && _query.TryGetPosition(id, out var position) ? position : origin,
				_ => throw new InvalidOperationException($"Unsupported query kind: {_kind}")
			};
		}
	}
}
