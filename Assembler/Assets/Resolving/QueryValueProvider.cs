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
		private readonly string _tag;
		private readonly IValueProvider<Vector3> _from;
		private readonly IValueProvider<float> _maxRange;

		public QueryValueProvider(
			EntityQueryService query,
			QueryKind kind,
			string tag,
			IValueProvider<Vector3> from,
			IValueProvider<float> maxRange)
		{
			_query = query;
			_kind = kind;
			_tag = tag;
			_from = from;
			_maxRange = maxRange;
		}

		public T Get(TriggerContext ctx) => (T)Read(ctx);

		object IValueProvider.Get(TriggerContext ctx) => Read(ctx);

		public void Set(T value) =>
			throw new InvalidOperationException("!query results are read-only and cannot be set.");

		private object Read(TriggerContext ctx)
		{
			var from = _from.Get(ctx);
			var id = _query.Nearest(from, _tag, _maxRange.Get(ctx));

			return _kind switch
			{
				QueryKind.NearestId => id ?? string.Empty,
				// No target: fall back to the From point so a Direction(self, target) reads as zero motion.
				QueryKind.NearestPosition => id != null ? _query.PositionOf(id) : from,
				_ => throw new InvalidOperationException($"Unsupported query kind: {_kind}")
			};
		}
	}
}
