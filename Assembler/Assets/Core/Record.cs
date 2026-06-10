using System.Collections.Generic;
using System.Linq;

namespace Assembler.Core
{
	/// <summary>
	/// A runtime item instance: a named bag of typed fields backing per-instance state (stack count,
	/// durability, rolled affixes, …). One shared CLR class for every declared pseudo-type — the schema
	/// lives in the descriptor, not in an emitted type — so it stays AOT/IL2CPP-safe in a player build.
	/// </summary>
	/// <remarks>
	/// Composes a private <see cref="Dictionary{TKey,TValue}"/> keyed by field name. The public
	/// <c>this[string]</c> indexer delegates to that dictionary, which is what the expression compiler's
	/// indexer support targets — giving native <c>item["count"]</c> reads/writes in descriptor
	/// expressions (reads return <see cref="object"/>, so a cast is needed; the cast-free
	/// <c>RecordHelper</c> helpers exist to avoid it). A <see cref="Record"/> is a reference type with
	/// default reference equality (no <c>Equals</c> override): an instance fetched from a
	/// <c>List&lt;Record&gt;</c> and mutated in place stays the same instance, and list
	/// <c>Remove</c>/<c>IndexOf</c> work by identity.
	/// </remarks>
	public sealed class Record
	{
		private readonly Dictionary<string, object> _fields;

		/// <summary>The declaring schema's name (for diagnostics and <see cref="ToString"/>).</summary>
		public string TypeName { get; }

		public Record(string typeName, IDictionary<string, object> fields)
		{
			TypeName = typeName;
			_fields = new Dictionary<string, object>(fields);
		}

		/// <summary>Reads or writes a field by name; delegates to the inner dictionary.</summary>
		public object this[string field]
		{
			get => _fields[field];
			set => _fields[field] = value;
		}

		/// <summary>The field names present on this instance, in no guaranteed order — for debug tooling that enumerates state.</summary>
		public IReadOnlyCollection<string> FieldNames => _fields.Keys;

		/// <summary>True when the named field is present on this instance.</summary>
		public bool ContainsField(string field) => _fields.ContainsKey(field);

		public override string ToString() =>
			$"{TypeName}{{ {string.Join(", ", _fields.Select(kvp => $"{kvp.Key}={kvp.Value}"))} }}";
	}
}
