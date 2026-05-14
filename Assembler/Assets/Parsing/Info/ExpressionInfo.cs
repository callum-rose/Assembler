using System.Collections.Generic;

namespace Assembler.Parsing.Info
{
	public record ExpressionInfo(string Id, IReadOnlyList<(string type, string name)> Arguments, string ReturnType, string Expression);
}