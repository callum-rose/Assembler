using System.Collections.Generic;

namespace Assembler.Parsing.Phase1.Dtos
{
    public sealed record BehaviourDto
    {
        public string? Type { get; init; }
        public string? Id { get; init; }
        public Dictionary<string, object>? Properties { get; init; }
    }
}
