using System.Collections.Generic;
using System.Linq;
using Assembler.Core;

namespace Assembler.Parsing.Info
{
	/// <summary>
	/// Bridges the <see cref="RecordValue"/> IR and the runtime <see cref="Record"/>. <see cref="ToRecord"/>
	/// builds a fresh <see cref="Record"/> from a completed <see cref="RecordValue"/>; the <c>Unwrap</c>/
	/// <c>Wrap</c> helpers move a single already-typed field value across the boundary (no schema). Only
	/// <see cref="ToRecord"/> is an extension — it is keyed on the domain-specific <see cref="RecordValue"/>;
	/// <c>Unwrap</c>/<c>Wrap</c> stay plain statics rather than extending broad types like
	/// <see cref="object"/>. Schema validation and default-filling happen earlier (transform time) via
	/// <c>RecordSchemaInfoExtensions.CreateInstance</c>.
	/// </summary>
	public static class RecordValueExtensions
	{
		public static Record ToRecord(this RecordValue record) => new(record.TypeName, Unwrap(record.Fields));

		public static Dictionary<string, object> Unwrap(IReadOnlyDictionary<string, AssemblerValue> fields) =>
			fields.ToDictionary(kvp => kvp.Key, kvp => UnwrapField(kvp.Value));

		public static AssemblerValue Wrap(object value) =>
			value switch
			{
				int i => new IntValue(i),
				float f => new FloatValue(f),
				bool b => new BoolValue(b),
				string s => new StringValue(s),
				_ => throw new ParsingException(
					$"Cannot store value '{value}' of type {value.GetType().Name} in a record field.")
			};

		private static object UnwrapField(AssemblerValue value) =>
			value switch
			{
				IntValue i => i.Value,
				FloatValue f => f.Value,
				BoolValue b => b.Value,
				StringValue s => s.Value,
				_ => throw new ParsingException(
					$"Record field holds an unsupported value '{value.GetType().Name}'; expected a scalar (int/float/bool/string).")
			};
	}
}
