namespace Assembler.Libraries
{
	/// <summary>
	/// Cast-free helpers for reading and writing <see cref="Record"/> fields from descriptor expressions.
	/// Registered globally in CompiledExpressionsRegistry so every expression can call these by bare name
	/// (GetInt, SetInt, HasField, …). They are sugar over the <see cref="Record"/> indexer: the getters
	/// avoid the <c>(int)item["count"]</c> cast the indexer otherwise forces, and the setters mutate the
	/// record in place and return it so they chain and can carry a side effect through a value-setter's
	/// <c>Value</c> slot. Both styles interoperate — they read and write the same underlying field bag.
	/// </summary>
	public static class RecordMath
	{
		/// <summary>Reads an int field.</summary>
		/// <param name="record">The record to read from.</param>
		/// <param name="field">The field name.</param>
		/// <returns>The field's value as an int.</returns>
		public static int GetInt(Record record, string field) => record[field] switch
		{
			int i => i,
			float f => (int)f,
			double d => (int)d,
			_ => 0
		};

		/// <summary>Reads a float field, widening a boxed int.</summary>
		/// <param name="record">The record to read from.</param>
		/// <param name="field">The field name.</param>
		/// <returns>The field's value as a float.</returns>
		public static float GetFloat(Record record, string field) => record[field] switch
		{
			float f => f,
			int i => i,
			double d => (float)d,
			_ => 0f
		};

		/// <summary>Reads a string field.</summary>
		/// <param name="record">The record to read from.</param>
		/// <param name="field">The field name.</param>
		/// <returns>The field's value as a string (never null).</returns>
		public static string GetString(Record record, string field) =>
			record[field] as string ?? record[field]?.ToString() ?? string.Empty;

		/// <summary>Reads a bool field.</summary>
		/// <param name="record">The record to read from.</param>
		/// <param name="field">The field name.</param>
		/// <returns>The field's value as a bool.</returns>
		public static bool GetBool(Record record, string field) => record[field] is true;

		/// <summary>Writes an int field in place and returns the record so calls can chain.</summary>
		/// <param name="record">The record to mutate.</param>
		/// <param name="field">The field name.</param>
		/// <param name="value">The value to store.</param>
		/// <returns>The same record instance.</returns>
		public static Record SetInt(Record record, string field, int value)
		{
			record[field] = value;
			return record;
		}

		/// <summary>Writes a float field in place and returns the record so calls can chain.</summary>
		/// <param name="record">The record to mutate.</param>
		/// <param name="field">The field name.</param>
		/// <param name="value">The value to store.</param>
		/// <returns>The same record instance.</returns>
		public static Record SetFloat(Record record, string field, float value)
		{
			record[field] = value;
			return record;
		}

		/// <summary>Writes a string field in place and returns the record so calls can chain.</summary>
		/// <param name="record">The record to mutate.</param>
		/// <param name="field">The field name.</param>
		/// <param name="value">The value to store.</param>
		/// <returns>The same record instance.</returns>
		public static Record SetString(Record record, string field, string value)
		{
			record[field] = value;
			return record;
		}

		/// <summary>Writes a bool field in place and returns the record so calls can chain.</summary>
		/// <param name="record">The record to mutate.</param>
		/// <param name="field">The field name.</param>
		/// <param name="value">The value to store.</param>
		/// <returns>The same record instance.</returns>
		public static Record SetBool(Record record, string field, bool value)
		{
			record[field] = value;
			return record;
		}

		/// <summary>True when the named field is present on the record.</summary>
		/// <param name="record">The record to test.</param>
		/// <param name="field">The field name.</param>
		/// <returns>Whether the field exists.</returns>
		public static bool HasField(Record record, string field) => record.ContainsField(field);
	}
}
