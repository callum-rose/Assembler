using System.Collections.Generic;

namespace Assembler.Deserialisation.Dtos
{
    public sealed record BehaviourDto
    {
        public string? Type { get; init; }
        public string? Id { get; init; }
        public List<string>? Tags { get; init; }
        public Dictionary<string, object>? Properties { get; init; }
        public List<ListenerDto>? Listeners { get; init; }
    }
}
