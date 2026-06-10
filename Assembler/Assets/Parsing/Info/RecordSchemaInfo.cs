using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Libraries;

namespace Assembler.Parsing.Info
{
	/// <summary>One declared field of a record schema: its name, CLR type, and optional explicit default.</summary>
	public sealed record RecordFieldInfo(string Name, Type ClrType, object? Default);

	/// <summary>
	/// A declared record pseudo-type: a name plus its typed fields. <see cref="CreateInstance"/> turns a
	/// (possibly partial) set of provided field values into a schema-complete <see cref="Record"/> — every
	/// declared field present and typed — applying defaults and validating at transform time, so every
	/// downstream unwrap site can build a <see cref="Record"/> with no schema lookup.
	/// </summary>
	public sealed record RecordSchemaInfo(string Name, IReadOnlyList<RecordFieldInfo> Fields)
	{
		/// <summary>
		/// Builds a complete, never-null <see cref="Record"/>: each declared field is materialised via the
		/// resolution order provided value → explicit default → type-zero (int→0, float→0f, bool→false,
		/// string→""). Throws <see cref="ParsingException"/> on an unknown field or a type mismatch. Record
		/// fields are never null — "absent" is modelled with a sentinel, not null.
		/// </summary>
		public Record CreateInstance(IReadOnlyDictionary<string, object> provided)
		{
			foreach (var key in provided.Keys)
			{
				if (Fields.All(f => f.Name != key))
				{
					throw new ParsingException(
						$"Record '{Name}' has no field '{key}'. Declared fields: {string.Join(", ", Fields.Select(f => f.Name))}.");
				}
			}

			var fields = new Dictionary<string, object>(Fields.Count);

			foreach (var field in Fields)
			{
				fields[field.Name] = provided.TryGetValue(field.Name, out var given)
					? Coerce(field, given)
					: field.Default is not null
						? Coerce(field, field.Default)
						: ZeroFor(field.ClrType);
			}

			return new Record(Name, fields);
		}

		private object Coerce(RecordFieldInfo field, object given)
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
					_ => throw Mismatch(field, given)
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

			throw Mismatch(field, given);
		}

		private ParsingException Mismatch(RecordFieldInfo field, object given) =>
			new($"Record '{Name}' field '{field.Name}' expects {field.ClrType.Name} but got value '{given}' of type {given.GetType().Name}.");

		private static object ZeroFor(Type clrType) =>
			clrType == typeof(int) ? 0 :
			clrType == typeof(float) ? 0f :
			clrType == typeof(bool) ? false :
			clrType == typeof(string) ? string.Empty :
			throw new ParsingException($"Record field type {clrType.Name} has no defined zero value.");
	}
}
