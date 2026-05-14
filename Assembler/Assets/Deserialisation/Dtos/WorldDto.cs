namespace Assembler.Deserialisation.Dtos
{
    public sealed record WorldDto
    {
        public int? Dimensionality { get; init; }
        public string? BackgroundColor { get; init; }
    }
}
