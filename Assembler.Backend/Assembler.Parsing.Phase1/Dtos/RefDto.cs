namespace Assembler.Parsing.Phase1.Dtos;

public abstract class RefDto
{
    public string Id { get; set; } = string.Empty;
}

public sealed class VarRefDto : RefDto;

public sealed class ConstRefDto : RefDto;