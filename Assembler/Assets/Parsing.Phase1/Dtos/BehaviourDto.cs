using System.Collections.Generic;

namespace Parsing.Phase1.Dtos
{
    public class BehaviourDto
    {
        public string? Type { get; set; }
        public string? Id { get; set; }
        public Dictionary<string, object>? Properties { get; set; }
    }
}
