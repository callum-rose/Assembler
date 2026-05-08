namespace Assembler.Parsing.Phase1.Dtos;

public class GameDto
{
    public InfoDto? Game { get; set; }
    public WorldDto? World { get; set; }
    public PhysicsDto? Physics { get; set; }
    public List<ValueDto>? Constants { get; set; }
    public List<ValueDto>? Variables { get; set; }
    public List<ExpressionDto>? Expressions { get; set; }
    public List<EntityDto>? Entities { get; set; }
}
