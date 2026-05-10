using System.Collections.Generic;

namespace Parsing.Phase1.Dtos
{
    public sealed record GameDto
    {
        public InfoDto? Game { get; init; }
        public WorldDto? World { get; init; }
        public PhysicsDto? Physics { get; init; }
        public List<ValueDto>? Constants { get; init; }
        public List<ValueDto>? Variables { get; init; }
        public List<ExpressionDto>? Expressions { get; init; }
        public List<EntityDto>? Entities { get; init; }
    }
}
