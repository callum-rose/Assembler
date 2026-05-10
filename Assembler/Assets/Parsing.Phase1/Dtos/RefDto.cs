namespace Parsing.Phase1.Dtos
{
    public abstract record RefDto
    {
        public string? Id { get; init; }
    }

    public sealed record VarRefDto : RefDto;

    public sealed record ConstRefDto : RefDto;

}