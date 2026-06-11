using System.Collections.Generic;

namespace Assembler.Parsing.Info
{
	/// <summary>
	/// An entity id that may be authored either as a literal string or as a template <c>!parameter</c>
	/// whose value is only known when the template is instantiated. This unifies the parameterisable
	/// entity-id substitution that used to be implemented three separate ways across listeners and the
	/// <c>!entity</c> tag (see issue #228): one <see cref="Resolve"/> routine now drives all of them.
	/// </summary>
	/// <remarks>
	/// A variable (<c>!var</c>) reference is resolved to a literal eagerly at parse time, so it needs no
	/// representation here — only the literal and the (deferred) parameter cases survive into the IR.
	/// </remarks>
	public abstract record ParameterisableEntityId(string Id)
	{
		/// <summary>The unresolved parameter name while this id is still pending, otherwise <c>null</c>.</summary>
		public abstract string? PendingParameter { get; }

		/// <summary>
		/// Resolves the id against the supplied template parameters. A literal passes through unchanged; a
		/// parameter bound to a string becomes a <see cref="LiteralEntityId"/>; an unbound parameter stays
		/// pending so a later substitution pass — or the resolve-time guard — can deal with it.
		/// </summary>
		public abstract ParameterisableEntityId Resolve(IReadOnlyDictionary<string, AssemblerValue> parameters);
	}

	/// <summary>A fully-known entity id.</summary>
	public sealed record LiteralEntityId(string Id) : ParameterisableEntityId(Id)
	{
		public override string? PendingParameter => null;

		public override ParameterisableEntityId Resolve(IReadOnlyDictionary<string, AssemblerValue> parameters) => this;
	}

	/// <summary>An entity id authored as <c>!parameter &lt;name&gt;</c>, resolved at template instantiation.</summary>
	public sealed record ParameterEntityId(string ParameterId) : ParameterisableEntityId(string.Empty)
	{
		public override string? PendingParameter => ParameterId;

		public override ParameterisableEntityId Resolve(IReadOnlyDictionary<string, AssemblerValue> parameters) =>
			parameters.TryGetValue(ParameterId, out var raw) && raw is StringValue sv
				? new LiteralEntityId(sv.Value)
				: this;
	}
}
