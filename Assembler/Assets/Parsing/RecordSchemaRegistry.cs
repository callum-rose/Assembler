using System.Collections.Generic;
using Assembler.Parsing.Info;

namespace Assembler.Parsing
{
	/// <summary>
	/// The record schemas declared under the top-level <c>Records:</c> section, keyed by schema name.
	/// Built once per transform and threaded onto <see cref="TransformContext"/>; consulted only at
	/// transform time (when a <c>!record</c> literal is materialised into a schema-complete
	/// <see cref="Assembler.Libraries.Record"/>), never at resolve time.
	/// </summary>
	public sealed class RecordSchemaRegistry
	{
		public readonly static RecordSchemaRegistry Empty = new(new Dictionary<string, RecordSchemaInfo>());

		private readonly IReadOnlyDictionary<string, RecordSchemaInfo> _schemas;

		public RecordSchemaRegistry(IReadOnlyDictionary<string, RecordSchemaInfo> schemas) => _schemas = schemas;

		public bool TryGet(string name, out RecordSchemaInfo? schema) => _schemas.TryGetValue(name, out schema);

		public RecordSchemaInfo Get(string name) =>
			_schemas.TryGetValue(name, out var schema)
				? schema
				: throw new ParsingException(
					$"Unknown record type '{name}'. Declare it under the top-level Records: section.");
	}
}
