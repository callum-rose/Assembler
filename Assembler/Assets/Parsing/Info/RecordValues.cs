using System.Collections.Generic;
using System.Linq;
using Assembler.Libraries;

namespace Assembler.Parsing.Info
{
	/// <summary>
	/// Bridges the <see cref="RecordValue"/> IR and the runtime <see cref="Record"/>. Unwrapping/wrapping a
	/// field is a plain primitive conversion (no schema): <see cref="Unwrap"/> reads a completed
	/// <see cref="RecordValue"/>'s fields into a plain object dict, <see cref="Wrap"/> boxes a field value
	/// back into a primitive <see cref="AssemblerValue"/>, and <see cref="ToRecord"/> builds a fresh
	/// <see cref="Record"/> from a completed <see cref="RecordValue"/>. Schema validation and default-filling
	/// happen earlier (transform time) via <see cref="RecordSchemaInfo.CreateInstance"/>; these helpers only
	/// move already-typed values across the boundary.
	/// </summary>
	public static class RecordValues
	{
		public static Record ToRecord(RecordValue record) => new(record.TypeName, Unwrap(record.Fields));

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
