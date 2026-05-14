namespace Assembler.Deserialisation.Dtos
{
    public sealed record ValueDto
    {
        public string? Id { get; init; }
        public object? Value { get; init; }
    }
}
