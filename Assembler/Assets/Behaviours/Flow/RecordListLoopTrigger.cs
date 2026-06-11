using Assembler.Core;

namespace Assembler.Behaviours.Flow
{
	/// <summary>Iterates a record list when Executed, firing listeners once per element. See <see cref="ListLoopTrigger{T}"/>.</summary>
	public class RecordListLoopTrigger : ListLoopTrigger<Record> { }
}
