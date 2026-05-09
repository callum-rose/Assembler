using System.Collections.Generic;

namespace Parsing.Phase1.Dtos
{
    public class EntityDto
    {
        public string? Id { get; set; }
        public List<string>? Tags { get; set; }
        public object? Position { get; set; }
        public object? Rotation { get; set; }
        public List<BehaviourDto>? Behaviours { get; set; }
    }
}
