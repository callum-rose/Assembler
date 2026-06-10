using System;
using System.Collections.Generic;

namespace Assembler.Parsing.Info
{
	/// <summary>One declared field of a record schema: its name, CLR type, and optional explicit default.</summary>
	public sealed record RecordFieldInfo(string Name, Type ClrType, object? Default);

	/// <summary>
	/// A declared record pseudo-type: a name plus its typed fields. Turning a (possibly partial) set of
	/// provided field values into a schema-complete <c>Record</c> is done by the
	/// <c>CreateInstance</c> extension method (see <see cref="RecordSchemaInfoExtensions"/>) — kept off the
	/// record itself so this stays pure data.
	/// </summary>
	public sealed record RecordSchemaInfo(string Name, IReadOnlyList<RecordFieldInfo> Fields);
}
