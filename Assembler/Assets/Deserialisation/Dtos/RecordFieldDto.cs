namespace Assembler.Deserialisation.Dtos
{
	// One field definition inside a `Records:` schema entry — e.g. `count: { Type: int, Default: 1 }`.
	// A schema is just a Dictionary<string, RecordFieldDto> (field name -> definition); there is no
	// schema-level metadata, so no wrapper DTO. Default is object? because it can be any scalar
	// (int/float/bool/string), parsed by ObjectNodeDeserializer like any other untyped value.
	public sealed record RecordFieldDto
	{
		public string? Type { get; init; }
		public object? Default { get; init; }
	}
}
