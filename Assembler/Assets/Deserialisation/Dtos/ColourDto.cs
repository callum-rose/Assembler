namespace Assembler.Deserialisation.Dtos
{
    public sealed record ColourDto
    {
        public object? R { get; init; }
        public object? G { get; init; }
        public object? B { get; init; }
        public object? A { get; init; }
        public string? Raw { get; init; }
    }
}
