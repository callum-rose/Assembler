namespace Parsing.Phase1.Dtos
{
    public sealed record WorldDto
    {
        public int? Dimensionality { get; init; }
        public string? BackgroundColor { get; init; }
    }
}
