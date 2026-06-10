using Assembler.Core;

namespace Assembler.Behaviours.ListOperations
{
	/// <summary>Removes the first occurrence (by reference identity) of a record from the target list when Executed. See <see cref="ListRemoveBehaviour{T}"/>.</summary>
	public class RecordListRemove : ListRemoveBehaviour<Record> { }
}
