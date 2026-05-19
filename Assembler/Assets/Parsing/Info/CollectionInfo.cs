using System.Collections.Generic;

namespace Assembler.Parsing.Info
{
	public record CollectionInfo(string Id, IReadOnlyList<string> Items);
}
