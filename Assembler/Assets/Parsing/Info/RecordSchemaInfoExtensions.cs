using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Core;

namespace Assembler.Parsing.Info
{
	/// <summary>
	/// Materialises a <see cref="RecordSchemaInfo"/> into a runtime <see cref="Record"/>. Kept as an
	/// extension so the schema record stays pure data.
	/// </summary>
	public static class RecordSchemaInfoExtensions
	{
		/// <summary>
		/// Builds a complete, never-null <see cref="Record"/>: each declared field is materialised via the
		/// resolution order provided value → explicit default → type-zero (int→0, float→0f, bool→false,
		/// string→""). Throws <see cref="ParsingException"/> on an unknown field or a type mismatch. Record
		/// fields are never null — "absent" is modelled with a sentinel, not null.
		/// </summary>
		public static Record CreateInstance(this RecordSchemaInfo schema, IReadOnlyDictionary<string, object> provided)
		{
			foreach (var key in provided.Keys)
			{
				if (schema.Fields.All(f => f.Name != key))
				{
					throw new ParsingException(
						$"Record '{schema.Name}' has no field '{key}'. Declared fields: {string.Join(", ", schema.Fields.Select(f => f.Name))}.");
				}
			}

			var fields = new Dictionary<string, object>(schema.Fields.Count);

			foreach (var field in schema.Fields)
			{
				fields[field.Name] = provided.TryGetValue(field.Name, out var given)
					? Coerce(schema, field, given)
					: field.Default is not null
						? Coerce(schema, field, field.Default)
						: ZeroFor(field.ClrType);
			}

			return new Record(schema.Name, fields);
		}

		private static object Coerce(RecordSchemaInfo schema, RecordFieldInfo field, object given)
		{
			if (field.ClrType == typeof(int) && given is int)
			{
				return given;
			}

			if (field.ClrType == typeof(float))
			{
				return given switch
				{
					float f => f,
					int i => (float)i,
					double d => (float)d,
					_ => throw Mismatch(schema, field, given)
				};
			}

			if (field.ClrType == typeof(bool) && given is bool)
			{
				return given;
			}

			if (field.ClrType == typeof(string) && given is string)
			{
				return given;
			}

			throw Mismatch(schema, field, given);
		}

		private static ParsingException Mismatch(RecordSchemaInfo schema, RecordFieldInfo field, object given) =>
			new($"Record '{schema.Name}' field '{field.Name}' expects {field.ClrType.Name} but got value '{given}' of type {given.GetType().Name}.");

		private static object ZeroFor(Type clrType) =>
			clrType == typeof(int) ? 0 :
			clrType == typeof(float) ? 0f :
			clrType == typeof(bool) ? false :
			clrType == typeof(string) ? string.Empty :
			throw new ParsingException($"Record field type {clrType.Name} has no defined zero value.");
	}
}
