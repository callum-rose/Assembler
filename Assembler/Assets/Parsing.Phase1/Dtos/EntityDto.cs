using System.Collections.Generic;

namespace Assembler.Parsing.Phase1.Dtos
{
    public sealed record EntityDto
    {
        public string? Id { get; init; }
        public List<string>? Tags { get; init; }
        public object? Position { get; init; }
        public object? Rotation { get; init; }
        public List<BehaviourDto>? Behaviours { get; init; }
    }
}
