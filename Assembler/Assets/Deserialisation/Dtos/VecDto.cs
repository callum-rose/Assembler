namespace Assembler.Deserialisation.Dtos
{
    public sealed record VecDto
    {
        public object? X { get; init; }
        public object? Y { get; init; }
        public object? Z { get; init; }
    }
}
