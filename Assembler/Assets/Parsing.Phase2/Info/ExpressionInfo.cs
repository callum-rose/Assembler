using System.Collections.Generic;

namespace Assembler.Parsing.Phase2.Parsing.Phase2.Info
{
	public record ExpressionInfo(string Id, IReadOnlyList<(string type, string name)> Arguments, string ReturnType, string Expression);
}