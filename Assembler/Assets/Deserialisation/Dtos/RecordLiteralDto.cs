using System.Collections.Generic;

namespace Assembler.Deserialisation.Dtos
{
	// A `!record { Type: Item, count: 3 }` literal: the schema name (the reserved `Type` key) plus the
	// explicitly-set field values keyed by field name. Field values are object (any scalar / tagged node)
	// and are resolved by the transformer against the named schema, with defaults applied for unset fields.
	public sealed record RecordLiteralDto
	{
		public string? Type { get; init; }
		public IReadOnlyDictionary<string, object> Fields { get; init; } = new Dictionary<string, object>();
	}
}
