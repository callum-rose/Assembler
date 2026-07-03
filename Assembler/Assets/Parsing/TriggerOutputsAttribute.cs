using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assembler.Parsing.Info;

namespace Assembler.Parsing
{
	/// <summary>
	/// Declares the trigger-output names a behaviour writes into the <c>TriggerContext</c> when it fires — the
	/// keys a downstream listener's <c>Outputs:</c> mapping is allowed to rename. This is the machine-readable
	/// source of truth for those names: it exists so the parsing layer has something to check an
	/// <c>Outputs:</c> mapping against, mirroring how a behaviour's property names come from its <c>*Info</c>
	/// record rather than from prose.
	/// </summary>
	/// <remarks>
	/// Applied to the behaviour's <see cref="BehaviourInfo"/> record so it is visible to both
	/// <see cref="ReferenceValidator"/> (which rejects a mapping that renames an output the behaviour never
	/// emits) and the <c>Behaviours.md</c> doc generator (which cross-checks the hand-authored <c>Outputs:</c>
	/// doc block against it, so the two cannot drift). Attach it only to a behaviour that mints a fresh context
	/// of its own — not to a gate/relay that forwards an upstream trigger's context unchanged, whose emitted
	/// keys are only knowable from upstream and so are deliberately left undeclared.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	public sealed class TriggerOutputsAttribute : Attribute
	{
		public TriggerOutputsAttribute(params string[] names) => Names = names;

		public IReadOnlyList<string> Names { get; }
	}

	/// <summary>
	/// Resolves the <see cref="TriggerOutputsAttribute"/>-declared output names for a behaviour. Returns an
	/// empty set for a behaviour that declares none (a relay, or a behaviour that emits nothing) — the caller
	/// treats "no declaration" as "not checkable" rather than "emits nothing", so relays never trip validation.
	/// </summary>
	public static class TriggerOutputs
	{
		public static IReadOnlyList<string> Declared(Type infoType) =>
			infoType.GetCustomAttribute<TriggerOutputsAttribute>()?.Names ?? Array.Empty<string>();

		public static IReadOnlyList<string> Declared(BehaviourInfo info) => Declared(info.GetType());

		public static bool Declares(Type infoType) => Declared(infoType).Any();
	}
}
